# MAGIS — Instalación y ejecución

## 1. Descripción

MAGIS es una aplicación web compuesta por dos proyectos de C# que trabajan en conjunto:

- **Magis.Api:** API que recibe las solicitudes de la aplicación y consulta la base de datos.
- **Magis.Web:** interfaz web que muestra las páginas al usuario y se comunica con `Magis.Api`.

Para utilizar el sistema deben estar en ejecución **los dos proyectos** y debe estar restaurada la base de datos en SQL Server.

## 2. Requisitos previos

Antes de iniciar, instala o verifica lo siguiente:

1. **Windows**.
2. **.NET 10 SDK** (los proyectos están configurados para `net10.0`).
3. **SQL Server LocalDB**, incluido normalmente con Visual Studio, o una instancia de SQL Server compatible.
4. **Visual Studio 2022** con la carga de trabajo de desarrollo de ASP.NET y web, compatible con .NET 10, o la CLI de .NET.
5. El archivo de respaldo de la base de datos de prueba.

## 3. Descargar el proyecto

Clona el repositorio desde GitHub o descarga el proyecto como ZIP y descomprímelo en una carpeta local.

```bash
git clone https://github.com/christophergiron/Proyecto-final-base-de-datos-I
```

## 4. Restaurar la base de datos 

1. Descarga el respaldo desde este enlace: https://drive.google.com/file/d/1giEmjU-buabY3p1ZQCKcvKhz8yzFPrhR/view?usp=sharing
2. Restaura el respaldo en SQL Server usando SQL Server Management Studio (SSMS) o la herramienta correspondiente al formato del archivo.
3. La base de datos debe quedar con el nombre **`MagisV2`**, porque es el nombre configurado en la cadena de conexión predeterminada.

## 5. Abrir la solución

Abre el archivo **`proyecto final base de datos 1.slnx`** desde Visual Studio. La solución incluye los proyectos `Magis.Api` y `Magis.Web`.

Si utilizas la terminal, entra en la carpeta raíz del repositorio y restaura las dependencias:

```bash
dotnet restore "proyecto final base de datos 1.slnx"
```

## 6. Ejecutar la aplicación

### Opción A: Visual Studio

1. Abre `proyecto final base de datos 1.slnx`.
2. En el Explorador de soluciones, haz clic derecho sobre la solución y selecciona **Configurar proyectos de inicio**.
3. Selecciona **Varios proyectos de inicio**.
4. Configura `Magis.Api` y `Magis.Web` con la acción **Iniciar**.
5. Acepta los cambios y ejecuta la solución con **F5** o **Ctrl + F5**.
6. Si Visual Studio abre la página web automáticamente, úsala. De lo contrario, visita `https://localhost:7131`.

### Opción B: Terminal

Abre dos terminales en la carpeta raíz del repositorio.

**Terminal 1 — API:**

```bash
dotnet run --project Magis.Api/Magis.Api.csproj --launch-profile https
```

La API debe quedar disponible en `https://localhost:7136`. Puedes comprobar que responde abriendo `https://localhost:7136/api`.

**Terminal 2 — Web:**

```bash
dotnet run --project Magis.Web/Magis.Web.csproj --launch-profile https
```

Abre en el navegador:

**https://localhost:7131**

No cierres las terminales mientras uses el sistema.
