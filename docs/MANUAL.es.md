# MarkdownLab 1.6.1 — Manual de usuario

MarkdownLab es un lector y editor Markdown para Windows de Sealsworn, bajo licencia MIT.

## Instalación
Descarga el instalador x64 o extrae el ZIP portátil y ejecuta MarkdownLab.exe. Requiere Microsoft Edge WebView2 Runtime; si no está instalado, descárgalo desde https://developer.microsoft.com/microsoft-edge/webview2/. El instalador permite registrar la aplicación en Abrir con para .md; Windows permite elegir la aplicación predeterminada desde Configuración. No requiere instalar .NET por separado.

## Leer y editar
Abre un archivo con Ctrl+O, arrástralo a la ventana o usa Abrir con. Elige Lectura, Edición o Dividida. El índice permite saltar a encabezados. Ajusta el zoom desde la barra inferior.

## Documentos
Usa + para una pestaña nueva y × para cerrarla. El asterisco indica cambios. Archivo permite abrir una carpeta (nivel actual), recientes, guardar todos y exportar. Ctrl+S guarda; Ctrl+Shift+S guarda como; Ctrl+W cierra la pestaña. Al cerrar con cambios puedes guardar, descartar o cancelar.

## Escritura
La barra de formato aplica encabezados, negrita, cursiva, tachado, listas, tareas, citas, código, enlaces, imágenes y tablas. Enter continúa una lista y Enter en un elemento vacío la termina. Ctrl+B e Ctrl+I aplican formato. Alt+flechas mueve una línea. Ctrl+F busca y Ctrl+H reemplaza todas las coincidencias. Inserta imágenes mediante URL o archivo local; mantén las rutas relativas al mover documentos. Arrastra imágenes al editor para insertar referencias.

## Preferencias y recuperación
Herramientas > Configuración permite elegir tema, ajuste de línea, idioma del corrector, restauración de pestañas y guardado. Por defecto se crean copias de recuperación cada 30 segundos; el autoguardado de originales está desactivado. El autoguardado opcional solo afecta archivos con nombre. Las copias están en %LOCALAPPDATA%\MarkdownLab\Workspace y no sustituyen una copia de seguridad externa.

## Corrector y exportación
Activa el corrector desde Ver o Configuración. Las sugerencias aparecen en el menú contextual; depende de los diccionarios instalados en Windows. El análisis tiene límites de tamaño y no reconoce toda la sintaxis Markdown. Archivo > Exportar HTML o PDF guarda una representación del documento. Comprueba el resultado, especialmente imágenes externas y documentos largos.

## Problemas y licencia
Si no se ve la lectura, comprueba WebView2 y reinicia. Consulta Ayuda > Acerca de para ver la versión. Informa de errores en https://github.com/Sealsworn/MarkdownLab/issues. Licencia MIT; consulta LICENSE y las licencias de las dependencias.

## Abrir desde Windows
MarkdownLab usa una sola instancia por usuario y sesión. Si ya está abierto, los archivos .md y .markdown recibidos desde el explorador se abren en pestañas; si un archivo ya está abierto se selecciona su pestaña. Si la ventana está minimizada se restaura.

## Actualizaciones
Ayuda > Buscar actualizaciones consulta la última release estable de GitHub. La comprobación al iniciar se puede desactivar en General. En instalaciones normales descarga el instalador previa confirmación y comprueba su SHA-256 publicado por GitHub; guarda los documentos antes de ejecutarlo. En modo portátil abre la página de descarga para actualizar manualmente.
