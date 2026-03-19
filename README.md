
# OpenUtau HTTP API Web Wrapper

This project attempts to provide a headless HTTP wrapper around the `OpenUtau.Core` namespace for easy interoperability in automated pipelines (such as converting midi, text, and generating synthetic songs locally via CLI/HTTP).

## Included Features
1. **System & Environment**
    - `GET /api/system/info`: Validates paths.
    - `GET /api/system/paths`: Logs data directory paths where dictionaries, models, and dictionaries are stored.

2. **Project Control**
    - `POST /api/project/render` (Upload a `.ustx` -> stream `audio/wav`).
    - `POST /api/project/formats/convert` (Upload a `.ust` / `.vsqx` / `.mid` -> Download `.ustx` JSON structure).
    - `POST /api/project/projectinfo/getinfo` (Upload a `.ustx` -> Download `.json` analysis containing Tracks, Singers, Renderers).
    - `POST /api/project/projectgenerate` (Upload JSON layout of Notes + Duration + Lyrics -> Generates & returns `.ustx`).

3. **G2P & Translations**
    - `GET /api/g2p`: Returns all default G2P languages known to the engine (e.g. `RussianG2p`).
    - `POST /api/g2p/{lang}/query`: Convert graphemes into phonemes.

4. **Singers / Renderers / Tools**
    - `GET /api/voicebanks`: Query parsed voicebank dictionaries.
    - `GET /api/phonemizers`: Get supported Phonemizer types.
    - `GET /api/tools/{type}`: View supported wavtools / resamplers.
    - `GET /api/renderers`: Show known renderers (e.g., `CLASSIC`, `VOGEN`, `ENUNU`).
    - `GET /api/singers`: Get exhaustive subbank information.

## Deployment

See [docs/deployment.md](docs/deployment.md) for runtime configuration, optional authentication, and deployment guidance.

### Next Steps
1. Add custom parameters (pitch, expression values).
2. Configure OpenUtau to natively resolve external resamplers by providing Linux/macOS paths via HTTP injection.
3. Hook DiffSinger and Voicevox dependencies automatically.

