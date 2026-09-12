const soundSourceCookieName = "pianomapper-sound-source";
const soundSourceCookieMaxAgeSeconds = 31536000;

export const defaultSoundSource = "piano";
export const externalMidiSoundSource = "external-midi";

export function isSoundSource(source) {
    return source === "synth" || source === "piano" || source === externalMidiSoundSource;
}

export function readSoundSourcePreference(cookieHeader) {
    const prefix = `${soundSourceCookieName}=`;
    const cookie = cookieHeader
        .split(";")
        .map(candidate => candidate.trim())
        .find(candidate => candidate.startsWith(prefix));
    const source = cookie?.slice(prefix.length);
    return isSoundSource(source) ? source : defaultSoundSource;
}

export function createSoundSourceCookie(source) {
    return `${soundSourceCookieName}=${source}; Max-Age=${soundSourceCookieMaxAgeSeconds}; Path=/; SameSite=Lax`;
}
