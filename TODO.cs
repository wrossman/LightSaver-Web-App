/*
-------------------
FINAL ITEMS
-------------------
- Enable HTTPS redirection (after SSL is configured).
- Review and check for OWASP Top 10 vulnerabilities.
- Update all Results.* responses to their appropriate HTTP responses.
- Remove all sensitive data from logging.
- Create Privacy Policy.
- Create Terms and Conditions.
- Check for uncaught exceptions.
- How can a user report their Roku lost or stolen?
- Verify that all images are being saved only at the maximum size the Roku device can handle.
- Remove unused dependencies.

-------------------
NEW FEATURE IDEAS
-------------------
- Create an album that lets you add pictures from multiple sources; share with friends via a link.

-------------------
DESIGN ITEMS
-------------------
- Create app logo.
- Create an uploading animation.
- On startup, display an image matching the splash screen, then fade it out so the splash screen appears to fade naturally.
- Create background for Roku.
- Web App error page creation.
- Web App code submission page.
- Web App home page with information.
- QR code for link to code submission.
- Add counter and upload animation.

-------------------
WEB APP TO-DO ITEMS
-------------------
- Encryption for image files.
- Update keys for resources on monthly intervals. Maybe less? Maybe more?
    Should I do it based on how many times they are accessed? Create an access counter property for the image.
- Limit uploading random garbage to endpoints.

-------------------
ROKU TO-DO ITEMS
-------------------
- Fix wacky lightroom parsing of overflow
- Handle all response codes from HttpClients.
- Find a way to get background image in parallel.
- Retry logic for failed connections.
- Limit the number of keys stored in Roku registry.

-------------------
DONE
-------------------
X Implement PBKDF2 for resource keys.
X Implement memory cache for handling user, Roku, and Lightroom update sessions; delete DbContext?
    Add plaintext keys to session in dictionary <guid, key>, property name: ResourcePackage.
X Change from comparing origin URI for changes in Lightroom album; just store it as a hash so it cannot be viewed.  
X Use GUID instead of image hash for storing on Roku device.  
X Keep a hash of the URL string so I can tell if the image URL has changed for Lightroom.  
X Add a prompt that the Lightroom album you linked has zero images and ask the user to try again.  
X Create separate polling endpoint for initial get.  
X Limit the number of images to store for each upload method.  
X Fix upload image source not taking a bunch of pictures.  
X Remove transfer file service and just set ready-to-transfer in the upload methods.  
X Expire Lightroom update sessions.  
X Pass device image dimensions to web app so it can set the max image size for each device.  
X Create a background for each image that is just the image but super blurred. Send the background with the image if Roku chooses the setting for a blurred image background.  
X Remove user and Roku sessions after flow failure.  
X Evaluate whether there’s a better approach to managing image resolution.  
X If the Lightroom album changes and there are a large number of images, the HTTP request will time out in the initial get.  
    For Google and Lightroom, image resolution is chosen based on the Roku device’s preferred size.  
    Upload image gets reformatted to the max size of the Roku device.  
X Expire user credentials at flow failure.  
X Set up antiforgery middleware.  
X Add fade-in animation for session code label, since it processes later.  
X Track if the session code provided has expired and then refresh.  
X Test session code expiration with Roku app.  
X Limit file size; the picture of Latvia doesn't load on Roku as a poster (likely too big).  
X Create class to manage session and resource expiration.  
X Check to see if Lightroom album has changed before sending the images.  
X Stop using HTML Agility Pack to serve HTML; find a better method.  
X Add a delete endpoint that lets you remove your files from Lightsaver.  
X Upload images from device.  
X Verify polling stops if you exit the "choose photos" page.  
X Add Lightroom scraping flow.  
X Add failure-to-upload-image page if cookies are disabled for upload-image flow.  
X Add site to select image source.  
X If a Roku tries to get Google Photos again, remove old photos before committing new ones.  
X Figure out why Google still shows the wrong project name.  
X Input validation for picture display time.  
X Require Roku to send a hashed version of its serial number; store IDs as hashes so I'm not storing people’s serials.  
X Check if Lightroom album doesn’t have any pictures before trying to display.  
X Fix picture display time causing slideshow issues if set too low.  
X Create a load config task instead of keeping it all in main scene init.  
X Add image display time to registry.  
X Fix wallpaper not showing if only one image.  
X If the keys Roku provides to Lightsaver web app are old, prompt it to redo the flow.  
X Ensure all polling tasks stop when leaving the screen in Roku.  
X Fix Roku pulling weird images after selecting new Google Photos from the Roku app.  
X Remove session code from images after linking and providing resource package.  
X Remove session code from session code hash set on session expiration.  
X Remove duplicate session if the same Roku device tries to connect to /roku.  
X Restrict direct access to the image store; create public access methods in the ImageStore class for images and links.  
X Fix null-handling issues throughout the workflow.  
X Decide whether the image hash should be used as the resource link. FOR NOW: YES.  
X Correct the stale session service timing.  
X Ensure all LogWarning() calls return something meaningful to the caller.  
X Provide access only when the user enters the correct key displayed by the Roku.  
X Update session timeout behavior.  
X Refactor static classes to enable dependency injection.  
X Set up proper logging for each stage of the workflow.  
X Add rate limiting to the endpoint that provides access to user photos.  
X If browser cookies fail, fall back to query parameters. (OAuth requires cookies, so this is not applicable.)

*/

