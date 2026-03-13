export async function postFinishUpload(): Promise<boolean> {

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL ?? "";

    const finishResponse = await fetch(`${SITE_BASE}/api/upload/finish-upload`, {
        method: "POST",
        credentials: "include",
    });

    return finishResponse.ok
}
