import { useState } from "react"
import fileStyles from "../styles/fileUpload.module.css"

type PostUploadButtonProps = {
    filesToUpload: File[];
    onClearImages: () => void;
}

export function PostUploadButton({ filesToUpload, onClearImages }: PostUploadButtonProps) {
    const [hovered, setHovered] = useState(false);



    return (
        <label className={`${fileStyles.fileUpload} ${fileStyles.postFileUpload}`}
            onMouseEnter={() => setHovered(true)}
            onMouseLeave={() => setHovered(false)}
            onClick={onClearImages}>
            {hovered
                ? "Clear Images"
                : `${filesToUpload.length} files ready to upload `}

        </label>
    );
}