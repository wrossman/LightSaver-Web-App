import { useUploadStatus } from "../hooks/useUploadStatus";
import { useNavigate } from "react-router-dom";

function UploadStatus() {

    const { uploadedImages, totalImages } = useUploadStatus();

    const nav = useNavigate();

    if (uploadedImages === totalImages && totalImages > 0) {
        nav("/UploadSuccess")
    }

    return (
        <div>
            <h1>Upload Status</h1>
            <p>{uploadedImages} of {totalImages} images uploaded...</p>
        </div>
    );
}

export default UploadStatus;