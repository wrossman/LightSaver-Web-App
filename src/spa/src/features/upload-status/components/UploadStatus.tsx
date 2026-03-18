import { useUploadStatus } from "../hooks/useUploadStatus";
import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer";
import "../../../shared/styles/styles.css"
import "../../../shared/styles/linkStyles.module.css"

function UploadStatus() {

    const { uploadedImages, totalImages } = useUploadStatus();

    const nav = useNavigate();

    useEffect(() => {
        if (uploadedImages === totalImages && totalImages > 0) {
            nav("/UploadSuccess")
        }
    }, [uploadedImages, totalImages, nav]);

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1 className="brandTitle">LightSaver</h1>
                <p>Upload Status</p>
                {totalImages === 0 ? null : <p>`${uploadedImages} of {totalImages} images uploaded...`</p>}
            </LinkContentContainer>
        </LinkContainer >
    );
}

export default UploadStatus;