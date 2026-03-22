import { BrandTitle } from "../../../shared/components/BrandTitle";
import { LinkContainer } from "../../../shared/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/components/LinkContentContainer";

function UploadSuccess() {
    return (
        <LinkContainer>
            <LinkContentContainer>
                <BrandTitle />
                <p>Upload Success</p>
            </LinkContentContainer>
        </LinkContainer>
    );
}

export default UploadSuccess;
