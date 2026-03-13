export async function postSessionCode(sessionCode: string): Promise<boolean> {

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL ?? "";

    const response = await fetch(`${SITE_BASE}/api/link/source`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            credentials: "include",
            body: JSON.stringify({ sessionCode: sessionCode })
        }
    );

    return response.ok;

}