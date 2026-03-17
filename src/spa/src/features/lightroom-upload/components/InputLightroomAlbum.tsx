import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { postLightroomAlbum } from "../api/PostLightroomAlbum";
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer";
import "../../../shared/styles/styles.css"
import "../../../shared/styles/linkStyles.module.css"

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
            </LinkContentContainer>
        </LinkContainer>
    );
}

export default InputLightroomAlbum;