import { useNavigate } from "react-router-dom";
import { getGoogleRedirect } from "../api/GetGoogleRedirect";
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import "../../../shared/styles/styles.css"
import "../../../shared/styles/linkStyles.module.css"
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer"
function SelectSource() {

    const nav = useNavigate()

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1>Select Source</h1>
                <button onClick={() => nav("?step=input-lightroom-album")}>Lightroom</button>
                <button onClick={async () => window.location.href = await getGoogleRedirect()}>Google</button>
                <button onClick={() => nav("?step=device-upload")}>Device Upload</button>
            </LinkContentContainer>
        </LinkContainer>

    );
}

export default SelectSource;