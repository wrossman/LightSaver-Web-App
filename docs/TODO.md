## Final Items
- Increase display time limit.
- Review for OWASP Top 10 vulnerabilities.
- Update `Results.*` responses to the appropriate HTTP status codes.
- Remove sensitive data from logging.
- Check for uncaught exceptions.
- Define the flow for reporting a lost or stolen Roku.
- Verify images are saved only at the maximum size the Roku device can handle.
- Remove unused dependencies.
- Replace link with QR code.

## New Feature Ideas
- Create an album that lets you add pictures from multiple sources and share it via a link.

## Design Items
- Add introduction page.
- Create privacy policy.
- Create terms and conditions.
- Add images to the startup screen.
- Create app logo.
- Create an uploading animation.
- Create a web app home page with information.
- Add counter and upload animation.

## Web App To-Do Items
- EB NGNIX proxy times out on the loading screen when uploading a bunch of images.
- Create unit tests.
- Add orphaned resource cleanup.
- Extract key and derivation creation into a method.
- Add constraints to width/height and device max size inputs from Roku ping endpoints.
- Add a message that pictures expire after a set time following upload.
- Warn when uploading unusually sized images.
- Verify how many items a Roku ID has in the resource database before uploading.
- Rotate resource keys on a schedule; consider access-based rotation using a per-image access counter.
- Reject invalid data uploads at endpoints.

## Roku To-Do Items
- Add retry logic so the stream doesn't just stop out of nowhere if there is a hiccup
- Prevent starting wallpapers while the Lightroom update is in progress. If the start menu runs while an update is needed, show a dialog indicating the album must be updated first.
- Add menu wallpaper components.
- Add option for a black background.
- Handle all response codes from HttpClients.
- Fetch background image in parallel.
- Add retry logic for failed connections.
- Limit the number of keys stored in the Roku registry.

## Done
- [x] On FHD screen, the background image does not fully render before fade in every time.
- [x] Fix FHD layouts
- [x] Encrypt image files. (Azure and AWS handle encryption at rest automatically in S3 and Blob.)
- [x] Write `imageShares` as a range instead of one at a time.
- [x] Add a QR code that links to the code submission page.
- [x] Create web app error page.
- [x] Create web app code submission page.
- [x] On startup, display an image matching the splash screen, then fade it out so the splash screen appears to fade naturally.
- [x] On the Get Photos screen, add upload instructions: "To upload images, scan the QR code or go to lightsaver.app and enter the code."
- [x] Enable HTTPS redirection (after SSL is configured).
- [x] Fix Lightroom parsing of overflow.
- [x] Before uploading, check whether the session is expired for each service.
- [x] Implement PBKDF2 for resource keys.
- [x] Implement memory cache for handling user, Roku, and Lightroom update sessions; evaluate whether DbContext can be removed.
  Add plaintext keys to session dictionary `<guid, key>` with property name `ResourcePackage`.
- [x] Instead of comparing origin URI changes in the Lightroom album, store a hash so it cannot be viewed.
- [x] Use GUID instead of image hash for storing on Roku device.
- [x] Keep a hash of the URL string to detect Lightroom image changes.
- [x] Add a prompt when the Lightroom album has zero images and ask the user to try again.
- [x] Create separate polling endpoint for initial get.
- [x] Limit the number of images to store for each upload method.
- [x] Fix upload image source not accepting multiple images.
- [x] Remove transfer file service and set ready-to-transfer in the upload methods.
- [x] Expire Lightroom update sessions.
- [x] Pass device image dimensions to web app so it can set the max image size for each device.
- [x] Create a background for each image that is the image but heavily blurred. Send the background with the image if Roku selects blurred background.
- [x] Remove user and Roku sessions after flow failure.
- [x] Evaluate whether there is a better approach to managing image resolution.
- [x] If the Lightroom album changes and there are a large number of images, the HTTP request times out during the initial get.
  For Google and Lightroom, image resolution is chosen based on the Roku device's preferred size.
  Upload image gets reformatted to the max size of the Roku device.
- [x] Expire user credentials at flow failure.
- [x] Set up antiforgery middleware.
- [x] Add fade-in animation for session code label, since it processes later.
- [x] Track if the session code provided has expired and then refresh.
- [x] Test session code expiration with Roku app.
- [x] Limit file size; a large test image does not load on Roku as a poster (likely too big).
- [x] Create class to manage session and resource expiration.
- [x] Check whether the Lightroom album has changed before sending the images.
- [x] Stop using HTML Agility Pack to serve HTML; find a better method.
- [x] Add a delete endpoint that lets you remove your files from LightSaver.
- [x] Upload images from device.
- [x] Verify polling stops if you exit the "choose photos" page.
- [x] Add Lightroom scraping flow.
- [x] Add failure-to-upload-image page if cookies are disabled for upload-image flow.
- [x] Add site to select image source.
- [x] If a Roku tries to get Google Photos again, remove old photos before committing new ones.
- [x] Figure out why Google still shows the wrong project name.
- [x] Add input validation for picture display time.
- [x] Require Roku to send a hashed version of its serial number; store IDs as hashes so I'm not storing people's serials.
- [x] Check if the Lightroom album doesn't have any pictures before trying to display.
- [x] Fix picture display time causing slideshow issues if set too low.
- [x] Create a load config task instead of keeping it all in main scene init.
- [x] Add image display time to registry.
- [x] Fix wallpaper not showing if only one image.
- [x] If the keys Roku provides to the LightSaver web app are old, prompt it to redo the flow.
- [x] Ensure all polling tasks stop when leaving the screen in Roku.
- [x] Fix Roku pulling weird images after selecting new Google Photos from the Roku app.
- [x] Remove session code from images after linking and providing resource package.
- [x] Remove session code from session code hash set on session expiration.
- [x] Remove duplicate session if the same Roku device tries to connect to `/roku`.
- [x] Restrict direct access to the image store; create public access methods in the ImageStore class for images and links.
- [x] Fix null-handling issues throughout the workflow.
- [x] Decision: use image hash as the resource link (current).
- [x] Correct the stale session service timing.
- [x] Ensure all `LogWarning()` calls return something meaningful to the caller.
- [x] Provide access only when the user enters the correct key displayed by the Roku.
- [x] Update session timeout behavior.
- [x] Refactor static classes to enable dependency injection.
- [x] Set up proper logging for each stage of the workflow.
- [x] Add rate limiting to the endpoint that provides access to user photos.
- [x] If browser cookies fail, fall back to query parameters. (OAuth requires cookies, so this is not applicable.)
