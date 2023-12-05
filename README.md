# UmbracoInstagramImport
Method to download instragram post from instagram feed

Here is a little guide to get up and running with this Instagram import.

Meta part:

1. Create a Meta app on https://developer.facebook.com/
2. Choose "Instagram Basic Display" from the products menu.
3. Go to: Instagram Basic Display > Basic Display.
4. Then scroll down to "User Token Generator".
5. Search for your instagram account and add it.
6. Login to your Instagram account.
7. Go to Settings > Apps & websites > Accept the invite.
8. Now you should see your account under "User Token Generator" in the Meta App.
9. There should now be a "Generate Token" button.
10. Generate the token and save it.

Umbraco Part:
1. Import the two .udt files.
2. Create a Instagram node at the root of your project. 
3. Create a folder in the media section, that should store all the media files from instagram.
4. Copy the ID from the media folder and insert it into the code (line 63).

Now you should be able to run the method (DownloadLatesInstagramPosts()).

![alt text](https://i.ibb.co/D1y9YkW/instagram-backoffice.png)
