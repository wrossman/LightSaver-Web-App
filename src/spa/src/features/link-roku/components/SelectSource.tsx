import { useNavigate } from "react-router-dom";
import { getGoogleRedirect } from "../api/GetGoogleRedirect";

function SelectSource() {

    const nav = useNavigate()

    return (
        <div>
            <h1>Select Source</h1>
            <button onClick={() => nav("?step=input-lightroom-album")}>Lightroom</button>
            <button onClick={async () => window.location.href = await getGoogleRedirect()}>Google</button>
            <button onClick={() => nav("?step=device-upload")}>Device Upload</button>
        </div>

    );
}

export default SelectSource;