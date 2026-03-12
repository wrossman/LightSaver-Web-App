import type { UploadStatusResponse } from "../types/UploadStatusResponse";

export async function getUploadStatus(): Promise<UploadStatusResponse> {

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;

    const response = await fetch(`${SITE_BASE}/api/link/upload-status`,
        {
            method: "GET",
            credentials: "include"
        }
    );

    if (!response.ok) {
        console.log("Failed to retrieve update status.");
        const empty: UploadStatusResponse = {
            uploadedImages: 0,
            totalImages: 0,
            status: ""
        }
        return empty;
    }

    const data: UploadStatusResponse = await response.json();
    console.log("Retrieved update status.")
    return data;
}