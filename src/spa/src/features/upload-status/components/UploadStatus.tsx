import { useUploadStatus } from "../hooks/useUploadStatus";
import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { LinkContainer } from "../../../shared/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/components/LinkContentContainer";
import "../../../shared/styles/styles.css"
import { BrandTitle } from "../../../shared/components/BrandTitle";

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
                <BrandTitle />
                <p>Upload Status</p>
                {totalImages === 0 ? <p></p> : (<p>{`${uploadedImages} of ${totalImages} images uploaded...`}</p>)}
            </LinkContentContainer>
        </LinkContainer >
    );
}

export default UploadStatus;
