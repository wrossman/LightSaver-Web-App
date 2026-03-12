import { useUploadStatus } from "../hooks/useUploadStatus";

function UploadStatus() {

    const { uploadedImages, totalImages } = useUploadStatus();

    return (
        <div>
            <h1>Upload Status</h1>
            <p>{uploadedImages} of {totalImages} images uploaded...</p>
        </div>
    );
}

export default UploadStatus;