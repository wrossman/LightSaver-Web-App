import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useDeviceUploadStatus } from "../hooks/useDeviceUploadStatus";
import fileStyles from "../styles/fileUpload.module.css"
import linkStyles from "../../../shared/styles/linkStyles.module.css"
import { LinkContainer } from "../../../shared/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/components/LinkContentContainer";
import { BrandTitle } from "../../../shared/components/BrandTitle";

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
                <BrandTitle />
                <form
                    className={linkStyles.linkForm}
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

                    {filesToUpload.length > 0
                        ? <p>{filesToUpload.length} files ready to upload</p>
                        : null}

                    <button className={linkStyles.linkButton} type="submit">Submit</button>
                    {uploadStatus.totalImages > 0
                        ? <p>{uploadStatus.currentUploaded} out of {uploadStatus.totalImages} images uploaded...</p>
                        : null}
                </form>
            </LinkContentContainer>
        </LinkContainer>

    );
}

export default DeviceUpload;
