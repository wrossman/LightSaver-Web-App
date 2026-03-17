import { useUploadStatus } from "../hooks/useUploadStatus";
import { useNavigate } from "react-router-dom";
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer";
import "../../../shared/styles/styles.css"
import "../../../shared/styles/linkStyles.module.css"

function UploadStatus() {

    const { uploadedImages, totalImages } = useUploadStatus();

    const nav = useNavigate();

    if (uploadedImages === totalImages && totalImages > 0) {
        nav("/UploadSuccess")
    }

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1>Upload Status</h1>
                <p>{uploadedImages} of {totalImages} images uploaded...</p>
            </LinkContentContainer>
        </LinkContainer >
    );
}

export default UploadStatus;