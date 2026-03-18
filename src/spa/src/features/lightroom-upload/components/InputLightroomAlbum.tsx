import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { postLightroomAlbum } from "../api/PostLightroomAlbum";
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer";
import linkStyles from "../../../shared/styles/linkStyles.module.css"

function InputLightroomAlbum() {

    const [albumLink, setAlbumLink] = useState("");
    const [error, setError] = useState("");

    const nav = useNavigate();

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()

        const success = await postLightroomAlbum(albumLink);

        if (success) {
            nav(`?step=upload-status`);
        }
        else {
            setError("Failed to link Lightroom album...");
        }

    }

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1 className={`brandTitle ${linkStyles.linkH1}`}>LightSaver</h1>
                <form className={linkStyles.linkForm} onSubmit={handleSubmit}>
                    <label>Enter Lightroom Album</label>
                    <input
                        className={linkStyles.linkInput}
                        type="text"
                        value={albumLink}
                        onChange={
                            (e) => {
                                setAlbumLink(e.target.value);
                                setError("");
                            }
                        }
                    />
                    <button className={linkStyles.linkButton} type="submit">Submit</button>

                    {error && <p>{error}</p>}

                </form>
            </LinkContentContainer>
        </LinkContainer >
    );
}

export default InputLightroomAlbum;
