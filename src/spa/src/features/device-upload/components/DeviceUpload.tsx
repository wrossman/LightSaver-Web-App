import { useState } from "react";
import { getCsrfToken } from "../../../shared/csrf";

function DeviceUpload() {

    const [filesToUpload, setFilesToUpload] = useState<File[]>([]);
    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;
    const [result, setResult] = useState("");

    function handleFileDrop(event: React.ChangeEvent<HTMLInputElement>) {
        const files = event.target.files

        if (files === null) return

        setFilesToUpload(Array.from(files));
    }

    async function handleSubmit(event: React.SubmitEvent) {
        event.preventDefault()

        const csrfToken = await getCsrfToken();

        for (const item of filesToUpload) {

            const formData = new FormData();
            formData.append('image', item);

            const response = await fetch(`${SITE_BASE}/api/upload/post-images`,
                {
                    method: 'POST',
                    body: formData,
                    credentials: 'include',
                    headers: {
                        "X-CSRF-TOKEN": csrfToken
                    }
                }
            )

            console.log(`Image uploaded: ${response.ok}`)

        };

        const finishResponse = await fetch(`${SITE_BASE}/api/upload/finish-upload`, {
            method: "POST",
            credentials: "include",
        })

        if (finishResponse.ok) {
            setResult("Upload Successful");
        }
        else {
            setResult("Upload Failed");
        }

    }

    return (
        <div>
            <form
                onSubmit={handleSubmit}
            >
                <h1>Device Upload</h1>

                <input
                    type="file"
                    accept="image/*"
                    multiple
                    onChange={handleFileDrop}>

                </input>
                <button type="submit">Submit</button>
                <p>{result}</p>
            </form>
        </div>

    );
}

export default DeviceUpload;