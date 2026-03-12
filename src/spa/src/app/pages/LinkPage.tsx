import { useSearchParams } from "react-router-dom";
import { cookiesEnabled } from "../../shared/verifyCookiesEnabled";
import SelectSource from "../../features/link-roku/components/SelectSource";
import InputLightroomAlbum from "../../features/lightroom-upload/components/InputLightroomAlbum";
import InputSessionCode from "../../features/link-roku/components/InputSessionCode";
import UploadStatus from "../../features/upload-status/components/UploadStatus";
import DeviceUpload from "../../features/device-upload/components/DeviceUpload";
import CookiesDisabled from "./error/CookiesDisabled";

export default function LinkPage() {
    const [searchParams] = useSearchParams();
    const step = searchParams.get("step") ?? "input-session-code";

    if (!cookiesEnabled())
        return <CookiesDisabled />

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