import { useEffect, useState } from "react";
import { getUploadStatus } from "../api/getUploadStatus";

export function useUploadStatus() {

    const [uploadedImages, setUploadedImages] = useState(0);
    const [totalImages, setTotalImages] = useState(0);

    useEffect(() => {

        async function pollUploadStatus(): Promise<void> {

            const statusData = await getUploadStatus();

            setUploadedImages(statusData.uploadedImages);
            setTotalImages(statusData.totalImages);

        }

        pollUploadStatus()


        const interval = setInterval(() => {

            pollUploadStatus()

        }, 1000);

        return () => clearInterval(interval);

    }, []);

    return { uploadedImages, totalImages };
}