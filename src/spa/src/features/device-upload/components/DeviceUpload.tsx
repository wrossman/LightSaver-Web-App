import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useDeviceUploadStatus } from "../hooks/useDeviceUploadStatus";
import "../../../shared/styles/styles.css"

function DeviceUpload() {

    const [filesToUpload, setFilesToUpload] = useState<File[]>([]);
    const { uploadStatus, startDeviceUpload } = useDeviceUploadStatus();

    const nav = useNavigate();

    function handleFileDrop(event: React.ChangeEvent<HTMLInputElement>) {
        const files = event.target.files

        if (files === null) return

        setFilesToUpload(Array.from(files));
    }

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()
        if (filesToUpload.length === 0) return;

        startDeviceUpload(filesToUpload);

    }

    if (uploadStatus.totalImages === uploadStatus.currentUploaded && uploadStatus.totalImages > 0) {
        nav("/UploadSuccess")
    }

    return (
        <div>
            <form
                onSubmit={handleSubmit}
            >
                <h1>Device Upload</h1>

                <input
                    type="file"
                    accept="image/*"
                    multiple
                    onChange={handleFileDrop}>

                </input>
                <button className="button" type="submit">Submit</button>
                <p>{uploadStatus.currentUploaded} out of {uploadStatus.totalImages} images uploaded...</p>
            </form>
        </div>

    );
}

export default DeviceUpload;
