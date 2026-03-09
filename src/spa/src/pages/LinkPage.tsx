import { useSearchParams } from "react-router-dom";
import SelectSource from "../components/link/SelectSource";
import InputLightroomAlbum from "../components/link/InputLightroomAlbum";
import InputSessionCode from "../components/link/InputSessionCode";
import UploadStatus from "../components/link/UploadStatus";
import DeviceUpload from "../components/link/DeviceUpload";

export default function LinkPage() {
    const [searchParams] = useSearchParams();
    const step = searchParams.get("step") ?? "input-session-code";

    return (
        <>
            {step === "select-source" && <SelectSource />}
            {step === "input-lightroom-album" && <InputLightroomAlbum />}
            {step === "upload-status" && <UploadStatus />}
            {step === "input-session-code" && <InputSessionCode />}
            {step === "device-upload" && <DeviceUpload />}
        </>
    );
}