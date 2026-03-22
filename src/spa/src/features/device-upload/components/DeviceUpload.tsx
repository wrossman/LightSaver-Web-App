import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useDeviceUploadStatus } from "../hooks/useDeviceUploadStatus";
import fileStyles from "../styles/fileUpload.module.css"
import linkStyles from "../../../shared/styles/linkStyles.module.css"
import { LinkContainer } from "../../../shared/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/components/LinkContentContainer";
import { BrandTitle } from "../../../shared/components/BrandTitle";
import { PreUploadButton } from "./PreUploadButton";
import { PostUploadButton } from "./PostUploadButton";
import type { UploadStep } from "../types/UploadStep";

function DeviceUpload() {

    const [filesToUpload, setFilesToUpload] = useState<File[]>([]);
    const { uploadStatus, startDeviceUpload } = useDeviceUploadStatus();
    const [uploadStep, setUploadStep] = useState<UploadStep>("idle");

    const nav = useNavigate();

    function handleFileDrop(event: React.ChangeEvent<HTMLInputElement>) {
        const files = event.target.files

        if (files === null) return

        setFilesToUpload(Array.from(files));
    }

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()
        if (filesToUpload.length === 0) return;

        setUploadStep("uploading");
        startDeviceUpload(filesToUpload);
    }

    function onClearImages() {
        setFilesToUpload([])
        setUploadStep("idle")
    };

    if (uploadStatus.totalImages === uploadStatus.currentUploaded && uploadStatus.totalImages > 0) {
        nav("/UploadSuccess")
    }

    let uploadButton;
    let uploadLabel;
    let uploadStatusSection;

    if (uploadStep == "idle") {
        uploadLabel = (
            <PreUploadButton
                handleFileDrop={handleFileDrop}
                setUploadStep={setUploadStep} />
        )
        uploadButton = (
            <button
                className={`${linkStyles.linkButton} ${fileStyles.uploadButtonInactive}`}
                type="submit">Upload Images
            </button>
        )
        uploadStatusSection = null;
    }
    else if (uploadStep == "ready") {
        uploadLabel = (
            <PostUploadButton
                filesToUpload={filesToUpload}
                onClearImages={onClearImages} />
        )
        uploadButton = (
            <button
                className={linkStyles.linkButton}
                type="submit">
                Upload Images
            </button>
        )
        uploadStatusSection = null;
    }
    else if (uploadStep == "uploading") {
        uploadLabel = null;
        uploadButton = null;
        uploadStatusSection = (
            <p>{uploadStatus.currentUploaded} out of {uploadStatus.totalImages} images uploaded...</p>
        )
    }

    return (
        <LinkContainer>
            <LinkContentContainer>
                <BrandTitle />
                <form
                    className={linkStyles.linkForm}
                    onSubmit={handleSubmit}>

                    <label>Device Upload</label>

                    {uploadLabel}
                    {uploadButton}
                    {uploadStatusSection}
                </form>
            </LinkContentContainer>
        </LinkContainer>

    );
}

export default DeviceUpload;
