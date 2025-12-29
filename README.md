# LightSaver

LightSaver is a Roku companion web app that lets you send personal photos to your device. The ASP.NET Core service exposes endpoints for linking a Roku, choosing a source (Google Photos, Lightroom, or manual upload), processing the images, and returning ready-to-display packages back to the TV.

## Features
- Session-based pairing so only your Roku can fetch the uploaded images.
- Multiple ingestion options (Google Photos Picker, Lightroom short code, local uploads).
- Image processing tailored to each Roku’s screen size, including blurred backgrounds.