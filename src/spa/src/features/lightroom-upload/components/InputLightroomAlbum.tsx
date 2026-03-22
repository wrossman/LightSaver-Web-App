import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { postLightroomAlbum } from "../api/PostLightroomAlbum";
import { LinkContainer } from "../../../shared/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/components/LinkContentContainer";
import { BrandTitle } from "../../../shared/components/BrandTitle";
import lrAlbumStyles from "../styles/lightroomUpload.module.css"
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
                <BrandTitle />
                <form className={linkStyles.linkForm} onSubmit={handleSubmit}>
                    <label>Enter Lightroom Album</label>
                    <div className={lrAlbumStyles.albumInputContainer}>
                        <p className={lrAlbumStyles.albumLinkHint}>https://adobe.ly/</p>
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
                        /></div>

                    <button className={linkStyles.linkButton} type="submit">Submit</button>

                    {error && <p>{error}</p>}

                </form>
            </LinkContentContainer>
        </LinkContainer >
    );
}

export default InputLightroomAlbum;
