import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { postSessionCode } from "../api/PostSessionCode";
import "../../../shared/styles/styles.css"
import "../../../shared/styles/linkStyles.module.css"
import { LinkContainer } from "../../../shared/styles/components/LinkContainer";
import { LinkContentContainer } from "../../../shared/styles/components/LinkContentContainer";

function InputSessionCode() {

    const [sessionCode, setSessionCode] = useState("");
    const [error, setError] = useState("");

    const nav = useNavigate();

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()

        const accepted = await postSessionCode(sessionCode);

        if (accepted) {
            nav(`?step=select-source`);
        }
        else {
            setError("Invalid Session Code...");
        }
    }

    return (
        <LinkContainer>
            <LinkContentContainer>
                <h1 className="brandTitle">LightSaver</h1>
                <p>Enter Session Code</p>
                <form onSubmit={handleSubmit}>
                    <div>
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
            </LinkContentContainer>
        </LinkContainer>
    );
}

export default InputSessionCode;