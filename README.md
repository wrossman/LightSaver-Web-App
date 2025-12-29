# LightSaver

LightSaver is a companion web application for Roku devices that allows users to upload and stream personal photos to their devices. The ASP.NET Core service exposes endpoints for linking a Roku device, selecting a photo source (Google Photos, Lightroom, or manual upload), processing images, and returning image IDs and access keys.

## Features
- Session-based pairing so only your Roku can fetch the uploaded images.
- Multiple ingestion options (Google Photos Picker, Lightroom short code, local uploads).
- Image processing tailored to each Roku’s screen size, including blurred backgrounds.
