import { useNavigate } from "react-router-dom";
import { getGoogleRedirect } from "../api/GetGoogleRedirect";
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import linkStyles from "../../../shared/styles/linkStyles.module.css"
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer"
function SelectSource() {

    const nav = useNavigate()

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1 className={`brandTitle ${linkStyles.linkH1}`}>LightSaver</h1>
                <p>Select Source</p>
                <button className={linkStyles.linkButton} onClick={() => nav("?step=input-lightroom-album")}>Lightroom</button>
                <button className={linkStyles.linkButton} onClick={async () => window.location.href = await getGoogleRedirect()}>Google</button>
                <button className={linkStyles.linkButton} onClick={() => nav("?step=device-upload")}>Device Upload</button>
            </LinkContentContainer>
        </LinkContainer>

    );
}

export default SelectSource;
