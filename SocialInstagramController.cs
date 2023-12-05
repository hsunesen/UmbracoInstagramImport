using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Website.Controllers;
using System.Web;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Models;
using Umbraco.Extensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Strings;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;
using project.Code.Models;

namespace project.Code.Controllers
{

    public class SocialInstagramController : SurfaceController
    {

        private readonly MediaFileManager _mediaFileManager;
        private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
        private readonly IWebHostEnvironment _environment;
        private IPublishedContent instagramNode;
        private IPublishedContent mediaNode;

        public SocialInstagramController(
              IUmbracoContextAccessor umbracoContextAccessor,
               IUmbracoDatabaseFactory databaseFactory,
               ServiceContext services,
               AppCaches appCaches,
               IProfilingLogger profilingLogger,
               IPublishedUrlProvider publishedUrlProvider, IWebHostEnvironment environment, MediaFileManager mediaFileManager, MediaUrlGeneratorCollection mediaUrlGenerators
               , IShortStringHelper shortStringHelper, IContentTypeBaseServiceProvider contentTypeBaseServiceProvider)
               : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _environment = environment;

            _mediaFileManager = mediaFileManager;
            _mediaUrlGenerators = mediaUrlGenerators;
            _shortStringHelper = shortStringHelper;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;

            //Find the instagram node in Umbraco
            instagramNode = UmbracoContext.Content.GetAtRoot().Where(x => x.IsDocumentType("instagram")).FirstOrDefault();
            
            //Set the value of the folder you want to store your instagram media files in.
            mediaNode = UmbracoContext.Media.GetById(3995);
        }

        //Run this method in a schedule task setup (Hangfire, url fetch etc.) 
        public async Task<bool> DownloadLatesInstagramPosts(bool forceUpdate)
        {

            //Find the time for the last token update
            DateTime lastUpdated = Convert.ToDateTime(instagramNode.Value("tokenLastUpdated").ToString());

            //If the tokenLastUpdated is set, then check if the token should be updated
            if (lastUpdated.Year != 0001)
            {
                //Has the token been updated in the last 50 days? if not, then update the token.
                if ((DateTime.Now.Date - lastUpdated.Date).TotalDays > 50)
                {
                    var newToken = UpdateInstagramToken(instagramNode.Value("accessToken").ToString());

                    IContentService contentService = Services.ContentService;

                    var instaNode = contentService.GetById(instagramNode.Id);
                    instaNode.SetValue("accessToken", newToken);
                    instaNode.SetValue("tokenLastUpdated", DateTime.Now);
                    contentService.SaveAndPublish(instaNode);
                }
            }

            //else go update the token
            else
            {
                var newToken = UpdateInstagramToken(instagramNode.Value("accessToken").ToString());

                IContentService contentService = Services.ContentService;

                var instaNode = contentService.GetById(instagramNode.Id);
                instaNode.SetValue("accessToken", newToken);
                instaNode.SetValue("tokenLastUpdated", DateTime.Now);
                contentService.SaveAndPublish(instaNode);
            }

            //Get the Instagram token from the Umbraco node
            string token = instagramNode.Value("accessToken").ToString();

            //Get the instagram userid with the token
            string userID = GetInstagramUserIDByAccessToken(token);


            using (WebClient client = new WebClient() { Encoding = Encoding.UTF8 })
            {
                string url = "https://graph.instagram.com/" + userID + "/media";

                client.QueryString.Add("fields", "id,caption,media_type,media_url,media_link,permalink,timestamp");
                client.QueryString.Add("access_token", token);

                var resultString = "";
                try
                {
                    resultString = client.DownloadString(url);
                    var instagramObject = JsonConvert.DeserializeObject<InstagramFeed>(resultString);
                    return await UpdateInstagramNodesInUmbracoAsync(instagramObject, forceUpdate);
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }

        public async Task<bool> UpdateInstagramNodesInUmbracoAsync(InstagramFeed instagramResult, bool forceUpdate)
        {
            if (instagramResult == null)
            {
                return false;
            }

            //Set how many posts you wanna import
            int limit = 10;

            foreach (var item in instagramResult.Data.Take(limit))
            {
                IContentService contentService = Services.ContentService;
                var exists = instagramNode.Children.Where(x => x.Value("postId").ToString() == item.Id).FirstOrDefault();

                //Force update of all posts
                if (forceUpdate)
                {
                    //The post already exists and should be updated
                    if (exists != null)
                    {
                        var post = contentService.GetById(exists.Id);

                        if (post.GetValue<DateTime>("timestamp") != item.Timestamp)
                        {
                            post.SetValue("postId", item.Id);
                            post.SetValue("caption", item.Caption);
                            post.SetValue("mediaType", item.MediaType);
                            post.SetValue("mediaUrl", item.MediaUrl);
                            post.SetValue("permalink", item.Permalink);
                            post.SetValue("timestamp", item.Timestamp);

                            //Download the media file from the post
                            var newImage = await CreateMediaAsync(item);

                            post.SetValue("imageFile", newImage.GetUdi());

                            contentService.SaveAndPublish(post);
                        }
                    }

                    //The Post does not exists, and should be created
                    else
                    {
                        var newPost = contentService.Create("Post - " + item.Timestamp.ToString("dd-MM-yyyy"), instagramNode.Id, "instagramPost");

                        newPost.SetValue("postId", item.Id);
                        newPost.SetValue("caption", item.Caption);
                        newPost.SetValue("mediaType", item.MediaType);
                        newPost.SetValue("mediaUrl", item.MediaUrl);
                        newPost.SetValue("permalink", item.Permalink);
                        newPost.SetValue("timestamp", item.Timestamp);

                        //Download the media file from the post
                        var newImage = await CreateMediaAsync(item);

                        newPost.SetValue("imageFile", newImage.GetUdi());
                        contentService.SaveAndPublish(newPost);
                    }
                }

                //Only create the new posts (no update of existing)
                else
                {
                    if (exists == null)
                    {
                        var newPost = contentService.Create("Post - " + item.Timestamp.ToString("dd-MM-yyyy"), instagramNode.Id, "instagramPost");

                        newPost.SetValue("postId", item.Id);
                        newPost.SetValue("caption", item.Caption);
                        newPost.SetValue("mediaType", item.MediaType);
                        newPost.SetValue("mediaUrl", item.MediaUrl);
                        newPost.SetValue("permalink", item.Permalink);
                        newPost.SetValue("timestamp", item.Timestamp);

                        //Download the media file from the post
                        var newImage = await CreateMediaAsync(item);

                        newPost.SetValue("imageFile", newImage.GetUdi());
                        contentService.SaveAndPublish(newPost);
                    }
                }
            }

            DeleteRemovedPosts(instagramResult);
            return true;
        }

        public string UpdateInstagramToken(string accessToken)
        {
            string url = "https://graph.instagram.com/refresh_access_token";

            using (WebClient client = new WebClient() { Encoding = Encoding.UTF8 })
            {
                client.QueryString.Add("grant_type", "ig_refresh_token");
                client.QueryString.Add("access_token", accessToken);

                var result = "";
                try
                {
                    var data = client.DownloadString(url);

                    result = JsonConvert.DeserializeObject<dynamic>(data).access_token;

                    return result;
                }
                catch (Exception ex)
                {
                    var err = ex.Message;

                    return result;
                }
            }
        }
        public string GetInstagramUserIDByAccessToken(string token)
        {
            string url = "https://graph.instagram.com/me";

            using (WebClient client = new WebClient() { Encoding = Encoding.UTF8 })
            {
                client.QueryString.Add("access_token", token);
                var result = "";
                try
                {
                    var data = client.DownloadString(url);

                    result = JsonConvert.DeserializeObject<dynamic>(data).id;

                    return result;
                }
                catch (Exception)
                {
                    return result;
                }
            }
        }

        public void DeleteRemovedPosts(InstagramFeed instagramResult)
        {
            var umbRoot = UmbracoContext.Content.GetAtRoot();

            var instagramNodes = instagramNode.Children;
            var tempInstagramData = instagramResult.Data;
            var nodesToDelete = new List<IPublishedContent>();
            foreach (IPublishedContent item in instagramNodes)
            {

                if (!tempInstagramData.Where(x => x.Id.ToString() == item.Value("postId").ToString()).Any())
                {
                    nodesToDelete.Add(item);
                }
            }

            if (nodesToDelete.Any())
            {
                IContentService contentService = Services.ContentService;
                foreach (var item in nodesToDelete)
                {
                    var nodeToDelete = contentService.GetById(item.Id);
                    contentService.Delete(nodeToDelete);
                }
            }

        }

        private async Task<string> DownloadMediaAsync(string url, string mediaTitle)
        {

            try
            {
                var client = new HttpClient();

                //Get the stream from the file url
                var response = await client.GetStreamAsync(url);

                //Define the path for the file
                string path = _environment.ContentRootPath + "/wwwroot/media/" + mediaTitle;

                //Create the file 
                using (var _fileStream = new FileStream(path, FileMode.Create))
                {
                    //Add the stream data to the file content
                    response.CopyTo(_fileStream);
                }

                //Returning the path of the file.
                return path;


            }
            catch (Exception ex)
            {
                var err = ex.Message;
                return null;
            }
        }

        private async Task<IMedia> CreateMediaAsync(Datum item)
        {
            
            bool isVideo = item.MediaType.ToLower().Equals("video");

            var fileEnding = (isVideo ? ".mp4" : ".jpg");

            //Get the folder, if it already exists one with the address name
            var mediaExists = mediaNode.Children.Where(x => x.Name.ToLower().Equals(item.Id + fileEnding));

            //Set media files 
            var mediaService = Services.MediaService;
            IMedia oldImage;

            if (mediaExists.Count() > 0)
            {
                oldImage = mediaService.GetById(mediaExists.FirstOrDefault().Id);
                if (oldImage != null)
                {
                    mediaService.Delete(oldImage, 0);
                }

                mediaService.EmptyRecycleBin(0);
            }

            var mediaPath = await DownloadMediaAsync(item.MediaUrl, item.Id + fileEnding);
            var newMedia = mediaService.CreateMedia(item.Id + fileEnding, mediaNode.Id, (isVideo ? "umbracoMediaVideo" : "Image"), 0);

            using (var imageStream = new FileStream(mediaPath, FileMode.Open))
            {
                newMedia.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, item.Id + fileEnding, imageStream);
                mediaService.Save(newMedia);
            }

            return newMedia;

        }

    }
}
