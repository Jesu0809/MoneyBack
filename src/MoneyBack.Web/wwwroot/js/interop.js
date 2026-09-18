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
