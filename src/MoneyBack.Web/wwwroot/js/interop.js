// Primer archivo de JS interop del proyecto. Hasta ahora ThemeService llamaba
// directo a globals del navegador (localStorage, document.documentElement)
// vía IJSRuntime sin necesitar un módulo propio — esto sí lo necesita, porque
// descargar un archivo requiere una secuencia de pasos (Blob, object URL, <a>
// sintético) que no tiene un equivalente de una sola llamada.
//
// Por qué existe esto en vez de un <a href> directo al endpoint del API: el
// token de autenticación (JWT) solo se adjunta a pedidos hechos con el
// HttpClient nombrado "Api" (vía AuthorizedHttpMessageHandler, del lado de
// Blazor) — una navegación de navegador a una URL nunca pasa por ahí y
// llegaría sin Authorization, así que el API respondería 401. Por eso
// ApiClient pide los bytes ya autenticado y esta función solo se encarga de
// la descarga real en el navegador con los bytes que ya llegaron.
export function downloadFile(bytes, nombreArchivo, tipoMime) {
    const blob = new Blob([new Uint8Array(bytes)], { type: tipoMime });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement("a");
    enlace.href = url;
    enlace.download = nombreArchivo;
    document.body.appendChild(enlace);
    enlace.click();
    document.body.removeChild(enlace);
    URL.revokeObjectURL(url);
}

// --- Generador de Atajo de iOS (.shortcut) ---
// Un .shortcut es un plist (formato de Apple) que describe una secuencia de
// acciones. Esto arma uno con el token ya metido adentro, para que en el
// iPhone sea solo "abrir el archivo descargado" -> Atajos ofrece agregarlo
// directo, sin que el usuario tenga que construirlo acción por acción.
// Validado con `plutil -lint`/`plutil -p` de macOS antes de integrarse aquí
// (mismo parser de plist que usa iOS), incluyendo el cálculo de rangos de
// attachmentsByRange (dónde va cada variable dentro del texto de la URL) —
// ese cálculo se hace por código, nunca a mano, porque un solo carácter de
// diferencia deja el archivo corrupto sin ningún aviso claro al importarlo.
function xmlEscape(texto) {
    return texto
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&apos;");
}

export function generarArchivoAtajo(apiBaseUrl, token) {
    const uuidCategoria = crypto.randomUUID().toUpperCase();
    const uuidMonto = crypto.randomUUID().toUpperCase();

    const PLACEHOLDER = "￼"; // Object Replacement Character: marca dónde va cada variable
    const urlBase = `${apiBaseUrl}/api/atajos/movimientos-por-nombre?categoriaNombre=`;
    const urlMid = "&monto=";

    const urlCompleta = urlBase + PLACEHOLDER + urlMid + PLACEHOLDER;
    const rangoCategoria = urlBase.length;
    const rangoMonto = urlBase.length + 1 + urlMid.length;

    const plist = `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>WFWorkflowActions</key>
    <array>
        <dict>
            <key>WFWorkflowActionIdentifier</key>
            <string>is.workflow.actions.ask</string>
            <key>WFWorkflowActionParameters</key>
            <dict>
                <key>WFAskActionPrompt</key>
                <string>¿Categoría? (ej. Comida, Transporte)</string>
                <key>WFInputType</key>
                <string>Text</string>
                <key>UUID</key>
                <string>${uuidCategoria}</string>
            </dict>
        </dict>
        <dict>
            <key>WFWorkflowActionIdentifier</key>
            <string>is.workflow.actions.ask</string>
            <key>WFWorkflowActionParameters</key>
            <dict>
                <key>WFAskActionPrompt</key>
                <string>¿Monto?</string>
                <key>WFInputType</key>
                <string>Number</string>
                <key>UUID</key>
                <string>${uuidMonto}</string>
            </dict>
        </dict>
        <dict>
            <key>WFWorkflowActionIdentifier</key>
            <string>is.workflow.actions.downloadurl</string>
            <key>WFWorkflowActionParameters</key>
            <dict>
                <key>WFURL</key>
                <dict>
                    <key>Value</key>
                    <dict>
                        <key>string</key>
                        <string>${xmlEscape(urlCompleta)}</string>
                        <key>attachmentsByRange</key>
                        <dict>
                            <key>{${rangoCategoria}, 1}</key>
                            <dict>
                                <key>Type</key>
                                <string>ActionOutput</string>
                                <key>OutputUUID</key>
                                <string>${uuidCategoria}</string>
                                <key>OutputName</key>
                                <string>Provided Input</string>
                            </dict>
                            <key>{${rangoMonto}, 1}</key>
                            <dict>
                                <key>Type</key>
                                <string>ActionOutput</string>
                                <key>OutputUUID</key>
                                <string>${uuidMonto}</string>
                                <key>OutputName</key>
                                <string>Provided Input</string>
                            </dict>
                        </dict>
                    </dict>
                    <key>WFSerializationType</key>
                    <string>WFTextTokenString</string>
                </dict>
                <key>WFHTTPMethod</key>
                <string>POST</string>
                <key>WFHTTPHeaders</key>
                <dict>
                    <key>Value</key>
                    <dict>
                        <key>WFDictionaryFieldValueItems</key>
                        <array>
                            <dict>
                                <key>WFItemType</key>
                                <integer>0</integer>
                                <key>WFKey</key>
                                <dict>
                                    <key>Value</key>
                                    <dict>
                                        <key>string</key>
                                        <string>X-Atajo-Token</string>
                                    </dict>
                                    <key>WFSerializationType</key>
                                    <string>WFTextTokenString</string>
                                </dict>
                                <key>WFValue</key>
                                <dict>
                                    <key>Value</key>
                                    <dict>
                                        <key>string</key>
                                        <string>${xmlEscape(token)}</string>
                                    </dict>
                                    <key>WFSerializationType</key>
                                    <string>WFTextTokenString</string>
                                </dict>
                            </dict>
                        </array>
                    </dict>
                    <key>WFSerializationType</key>
                    <string>WFDictionaryFieldValue</string>
                </dict>
            </dict>
        </dict>
        <dict>
            <key>WFWorkflowActionIdentifier</key>
            <string>is.workflow.actions.showresult</string>
            <key>WFWorkflowActionParameters</key>
            <dict/>
        </dict>
    </array>
    <key>WFWorkflowClientVersion</key>
    <string>1128.0.4</string>
    <key>WFWorkflowIcon</key>
    <dict>
        <key>WFWorkflowIconStartColor</key>
        <integer>4271458815</integer>
        <key>WFWorkflowIconGlyphNumber</key>
        <integer>59511</integer>
    </dict>
    <key>WFWorkflowImportQuestions</key>
    <array/>
    <key>WFWorkflowInputContentItemClasses</key>
    <array/>
    <key>WFWorkflowMinimumClientVersion</key>
    <integer>900</integer>
    <key>WFWorkflowTypes</key>
    <array>
        <string>ActionExtension</string>
        <string>NCWidget</string>
        <string>WatchKit</string>
    </array>
</dict>
</plist>
`;

    const bytes = new TextEncoder().encode(plist);
    downloadFile(Array.from(bytes), "MoneyBack - Agregar movimiento.shortcut", "application/octet-stream");
}

export async function copiarAlPortapapeles(texto) {
    try {
        await navigator.clipboard.writeText(texto);
        return true;
    } catch {
        return false;
    }
}

// --- Notificaciones push ---
// pushManager.subscribe() exige la llave VAPID como Uint8Array, pero el
// backend la entrega en base64url (formato estándar de VAPID) — este es el
// conversor de una sola vía que traduce entre los dos formatos.
function base64UrlAUint8Array(base64Url) {
    const padding = "=".repeat((4 - (base64Url.length % 4)) % 4);
    const base64 = (base64Url + padding).replace(/-/g, "+").replace(/_/g, "/");
    const raw = atob(base64);
    return Uint8Array.from([...raw].map((c) => c.charCodeAt(0)));
}

export function notificacionesSoportadas() {
    return "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}

export function estadoPermisoNotificaciones() {
    return notificacionesSoportadas() ? Notification.permission : "unsupported";
}

// Devuelve { endpoint, p256dh, auth } o null si el usuario negó el permiso o
// el navegador rechazó el registro (ej. modo incógnito, sin conexión). El
// permiso SOLO se puede pedir a raíz de un gesto del usuario (un click) —
// por eso esto se llama desde un botón "Activar notificaciones", nunca solo.
// Atrapa cualquier error de las llamadas al navegador: si esto lanza sin
// capturar, Blazor lo trata como excepción no manejada y tumba el render
// completo del componente, no solo esta acción puntual.
export async function pedirPermisoYSuscribirse(vapidPublicKeyBase64) {
    try {
        if (!notificacionesSoportadas()) return null;

        const permiso = await Notification.requestPermission();
        if (permiso !== "granted") return null;

        const registro = await navigator.serviceWorker.register("/service-worker.js");
        await navigator.serviceWorker.ready;

        let suscripcion = await registro.pushManager.getSubscription();
        if (!suscripcion) {
            suscripcion = await registro.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: base64UrlAUint8Array(vapidPublicKeyBase64)
            });
        }

        const json = suscripcion.toJSON();
        return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
    } catch (error) {
        console.error("No se pudo activar notificaciones push:", error);
        return null;
    }
}

export async function estaSuscritoNotificaciones() {
    if (!notificacionesSoportadas()) return false;
    const registro = await navigator.serviceWorker.getRegistration();
    const suscripcion = await registro?.pushManager.getSubscription();
    return !!suscripcion;
}

export async function desuscribirseNotificaciones() {
    try {
        if (!notificacionesSoportadas()) return null;
        const registro = await navigator.serviceWorker.getRegistration();
        const suscripcion = await registro?.pushManager.getSubscription();
        if (!suscripcion) return null;

        const json = suscripcion.toJSON();
        await suscripcion.unsubscribe();
        return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
    } catch (error) {
        console.error("No se pudo desactivar notificaciones push:", error);
        return null;
    }
}
