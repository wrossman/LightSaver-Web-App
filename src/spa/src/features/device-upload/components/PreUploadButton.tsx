import fileStyles from "../styles/fileUpload.module.css"
import type { Dispatch, SetStateAction } from "react";
import type { UploadStep } from "../types/UploadStep";

type PreUploadButtonProps = {
    handleFileDrop: React.ChangeEventHandler<HTMLInputElement>;
    setUploadStep: Dispatch<SetStateAction<UploadStep>>
};

export function PreUploadButton({ handleFileDrop, setUploadStep }: PreUploadButtonProps) {
    return (
        <label className={`${fileStyles.fileUpload} ${fileStyles.preFileUpload}`}>
            Select Images
            <input
                type="file"
                accept="image/*"
                multiple
                onChange={(e) => {
                    handleFileDrop(e);
                    setUploadStep("ready")
                }}>
            </input>
        </label >)

}