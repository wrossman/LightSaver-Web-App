import { useNavigate } from "react-router-dom";
import { getGoogleRedirect } from "../api/GetGoogleRedirect";
import { LinkContainer } from "../../../shared/components/LinkContainer";
import linkStyles from "../../../shared/styles/linkStyles.module.css"
import { LinkContentContainer } from "../../../shared/components/LinkContentContainer"
import { BrandTitle } from "../../../shared/components/BrandTitle";

function SelectSource() {

    const nav = useNavigate()

    return (
        <LinkContainer>
            <LinkContentContainer>
                <BrandTitle />
                <p>Select Source</p>
                <button className={linkStyles.linkButton} onClick={() => nav("?step=input-lightroom-album")}>Lightroom</button>
                <button className={linkStyles.linkButton} onClick={async () => window.location.href = await getGoogleRedirect()}>Google</button>
                <button className={linkStyles.linkButton} onClick={() => nav("?step=device-upload")}>Device Upload</button>
            </LinkContentContainer>
        </LinkContainer>

    );
}

export default SelectSource;
