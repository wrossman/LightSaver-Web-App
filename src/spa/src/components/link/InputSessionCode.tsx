import { useState } from "react";
import { useNavigate } from "react-router-dom";

function InputSessionCode() {

    const [sessionCode, setSessionCode] = useState("");
    const [error, setError] = useState("");
    const [cookiesBlocked, setCookiesBlocked] = useState(false);

    const nav = useNavigate();

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()

        if (!cookiesEnabled()) {
            console.log("Cookies are disabled");
            setCookiesBlocked(true);
            return;
        }
        else {
            console.log("Cookies are enabled");
        }

        const response = await fetch(`${SITE_BASE}/api/link/source`,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({ sessionCode: sessionCode })
            }
        );

        if (!response.ok) {
            setError("Invalid Session Code");
            return;
        }

        nav(`?step=select-source`);
    }

    if (cookiesBlocked)
        return (
            <h1>Please enable Cookies to link photos.</h1>)

    return (
        <form onSubmit={handleSubmit}>
            <div>
                <label>Session Code:</label>
                <input
                    type="text"
                    value={sessionCode}
                    onChange={
                        (e) => {
                            setSessionCode(e.target.value);
                            setError("");
                        }
                    }
                />
            </div>
            <button type="submit">Submit</button>

            <p>{error}</p>

        </form>
    );
}

function cookiesEnabled(): boolean {
    const name = `cookie_test_${Date.now()}`;

    document.cookie = `${name}=1; path=/`;

    const enabled = document.cookie.includes(`${name}=`);

    document.cookie = `${name}=; Max-Age=0; path=/`;

    return enabled;
}

export default InputSessionCode;