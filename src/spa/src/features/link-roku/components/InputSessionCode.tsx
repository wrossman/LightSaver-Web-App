import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { postSessionCode } from "../api/PostSessionCode";
import "../../../shared/styles/styles.css"
import linkStyles from "../../../shared/styles/linkStyles.module.css"
import { LinkContainer } from "../../../shared/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/components/LinkContentContainer";

function InputSessionCode() {

    const [sessionCode, setSessionCode] = useState("");

    const nav = useNavigate();

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()

        const accepted = await postSessionCode(sessionCode);

        if (accepted) {
            nav(`?step=select-source`);
        }
        else {
            // setError("Invalid Session Code...");
        }
    }

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1 className={`brandTitle ${linkStyles.linkH1}`}>LightSaver</h1>
                <form className={linkStyles.linkForm} onSubmit={handleSubmit}>
                    <label>Enter Session Code</label>
                    <input
                        className={linkStyles.linkInput}
                        type="text"
                        placeholder="ABC1234"
                        value={sessionCode}
                        maxLength={7}
                        onChange={
                            (e) => {
                                setSessionCode(e.target.value.toUpperCase());
                                // setError("");
                            }
                        }
                    />
                    <button className={linkStyles.linkButton} type="submit">Submit</button>

                </form>
            </LinkContentContainer>
        </LinkContainer>
    );
}

export default InputSessionCode;
