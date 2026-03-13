import { useState } from "react";
import { getCsrfToken } from "../../../shared/api/getCsrfToken";
import { postFinishUpload } from "../api/postFinishUpload";
import { postUploadImage } from "../api/postUploadImage";
import type { DeviceUploadStatus } from "../types/DeviceUploadStatus";

export function useDeviceUploadStatus() {

    const [uploadStatus, setUploadStatus] = useState<DeviceUploadStatus>({
        isUploading: false,
        currentUploaded: 0,
        totalImages: 0,
        error: null
    });

    async function startDeviceUpload(images: File[]) {

        setUploadStatus({
            isUploading: true,
            currentUploaded: 0,
            totalImages: images.length,
            error: null
        })

        const csrfToken = await getCsrfToken();

        if (csrfToken === "") {
            setUploadStatus((prev) => ({
                ...prev,
                error: "Failed to retrieve CSRF token from server."
            }));
            return;
        }

        for (const image of images) {
            const postImageSuccess = await postUploadImage(image, csrfToken);

            if (!postImageSuccess) {
                setUploadStatus((prev) => ({
                    ...prev,
                    error: `Failed to upload image number ${prev.currentUploaded + 1}`
                }));
                return;
            };

            setUploadStatus((prev) => ({
                ...prev,
                currentUploaded: prev.currentUploaded + 1
            }));

        }

        const accepted = await postFinishUpload();

        if (!accepted) {
            setUploadStatus((prev) => ({
                ...prev,
                error: "Failed to Post to finish upload endpoint."
            }));
            return;
        }

        setUploadStatus((prev) => ({
            ...prev,
            isUploading: false
        }));
    }

    return {
        uploadStatus,
        startDeviceUpload
    }
}