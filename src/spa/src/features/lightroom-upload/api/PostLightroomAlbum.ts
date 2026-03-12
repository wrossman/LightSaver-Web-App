export async function postLightroomAlbum(albumLink: string): Promise<boolean> {

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;

    const response = await fetch(`${SITE_BASE}/api/lightroom/post-album`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            credentials: "include",
            body: JSON.stringify({ albumLink })
        }
    );

    return response.ok

}