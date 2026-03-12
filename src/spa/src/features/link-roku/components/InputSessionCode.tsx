import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { postSessionCode } from "../api/PostSessionCode";

function InputSessionCode() {

    const [sessionCode, setSessionCode] = useState("");
    const [error, setError] = useState("");

    const nav = useNavigate();

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()

        const postResult = await postSessionCode(sessionCode);

        if (postResult) {
            nav(`?step=select-source`);
        }
        else {
            setError("Invalid Session Code...");
        }
    }

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



export default InputSessionCode;