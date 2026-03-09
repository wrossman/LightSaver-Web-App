import { useEffect, useState } from "react";

interface UploadStatusResponse {
    uploadedImages: number
    totalImages: number
    status: string
}

async function checkUploadStatus(): Promise<UploadStatusResponse> {

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

function UploadStatus() {

    const [uploadedImages, setUploadedImages] = useState(0);
    const [totalImages, setTotalImages] = useState(0);

    useEffect(() => {

        async function pollUploadStatus(): Promise<void> {

            const statusData = await checkUploadStatus();

            setUploadedImages(statusData.uploadedImages);
            setTotalImages(statusData.totalImages);
            console.log(statusData.uploadedImages);
            console.log(statusData.totalImages);
        }

        pollUploadStatus()


        const interval = setInterval(() => {

            pollUploadStatus()

        }, 1000);

        return () => clearInterval(interval);

    }, []);

    return (
        <div>
            <h1>Upload Status</h1>
            <p>{uploadedImages} of {totalImages} images uploaded...</p>
        </div>

    );
}

export default UploadStatus;