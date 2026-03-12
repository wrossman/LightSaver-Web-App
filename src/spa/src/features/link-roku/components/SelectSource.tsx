import { useNavigate } from "react-router-dom";

interface GoogleRedirectUrl {
    url: string;
}

function SelectSource() {

    const nav = useNavigate()

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;

    async function googleRedirect() {

        const response = await fetch(`${SITE_BASE}/api/google/google-redirect`,
            {
                method: "GET",
                credentials: "include"
            }
        )

        const DATA: GoogleRedirectUrl = await response.json();

        window.location.replace(DATA.url);

    }
    return (
        <div>
            <h1>Select Source</h1>
            <button onClick={() => nav("?step=input-lightroom-album")}>Lightroom</button>
            <button onClick={() => googleRedirect()}>Google</button>
            <button onClick={() => nav("?step=device-upload")}>Device Upload</button>
        </div>

    );
}

export default SelectSource;