import { useNavigate } from "react-router-dom";
import { getGoogleRedirect } from "../api/GetGoogleRedirect";
import { LinkContainer } from "../../shared/components/LinkContainer";
import linkStyles from "../../shared/styles/linkStyles.module.css"
import { LinkContentContainer } from "../../shared/components/LinkContentContainer"
function SelectSource() {

    const nav = useNavigate()

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1>Select Source</h1>
                <button className={linkStyles.button} onClick={() => nav("?step=input-lightroom-album")}>Lightroom</button>
                <button className={linkStyles.button} onClick={async () => window.location.href = await getGoogleRedirect()}>Google</button>
                <button className={linkStyles.button} onClick={() => nav("?step=device-upload")}>Device Upload</button>
            </LinkContentContainer>
        </LinkContainer>

    );
}

export default SelectSource;