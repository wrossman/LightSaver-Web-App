import { useState } from "react";
import { useNavigate } from "react-router-dom";

function InputLightroomAlbum() {

    const [albumLink, setAlbumLink] = useState("");
    const [error, setError] = useState("");

    const nav = useNavigate();

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()



        const response = await fetch(`${SITE_BASE}/api/lightroom/post-album`,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                credentials: "include",
                body: JSON.stringify({ albumLink })
            }
        );

        if (!response.ok) {
            setError("Failed to link album.");
            return;
        }

        nav(`?step=upload-status`);
    }

    return (
        <form onSubmit={handleSubmit}>
            <div>
                <label>Lightroom Album:</label>
                <input
                    type="text"
                    value={albumLink}
                    onChange={
                        (e) => {
                            setAlbumLink(e.target.value);
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

export default InputLightroomAlbum;