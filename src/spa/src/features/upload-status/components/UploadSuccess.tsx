import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer";
import linkStyles from "../../../shared/styles/linkStyles.module.css"

function UploadSuccess() {
    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1 className={`brandTitle ${linkStyles.linkH1}`}>LightSaver</h1>
                <p>Upload Success</p>
            </LinkContentContainer>
        </LinkContainer>
    );
}

export default UploadSuccess;
