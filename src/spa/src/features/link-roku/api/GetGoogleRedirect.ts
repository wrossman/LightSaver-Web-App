import type { GetGoogleRedirectResponse } from "../types/GetGoogleRedirectResponse";

export async function getGoogleRedirect(): Promise<string> {

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL ?? "";

    const response = await fetch(`${SITE_BASE}/api/google/google-redirect`,
        {
            method: "GET",
            credentials: "include"
        }
    )

    const data: GetGoogleRedirectResponse = await response.json();

    return data.url;

}