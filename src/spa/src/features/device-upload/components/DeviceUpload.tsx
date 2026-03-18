import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useDeviceUploadStatus } from "../hooks/useDeviceUploadStatus";
import fileStyles from "../styles/fileUpload.module.css"
import "../../../shared/styles/linkStyles.module.css"
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer";

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
        <LinkContainer>
            <LinkContentContainer>
                <h1 className="brandTitle">LightSaver</h1>
                <form
                    onSubmit={handleSubmit}>

                    <label>Device Upload</label>

                    <label className={fileStyles.fileUpload}>
                        Upload Images
                        <input
                            type="file"
                            accept="image/*"
                            multiple
                            onChange={handleFileDrop}>
                        </input>
                    </label>

                    <button type="submit">Submit</button>
                    {uploadStatus.totalImages > 0
                        ? <p>{uploadStatus.currentUploaded} out of {uploadStatus.totalImages} images uploaded...</p>
                        : null}
                </form>
            </LinkContentContainer>
        </LinkContainer>

    );
}

export default DeviceUpload;
