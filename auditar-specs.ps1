<#
    auditar-specs.ps1

    Compara el estado que cada spec declara en prosa contra el que specs/README.md
    publica en su indice.

    Por que un modelo y no una regex: specs/formato-spec.md §4.1 lo dice explicito --
    "El estado no es dato, es prosa. Conviven `**Estado:** implementado`,
    `- **Estado:** **propuesto**` y `- **Estado:** implementado.`". Una regex sobre tres
    redacciones distintas se rompe con la cuarta. Extraer el estado de la prosa es
    justamente lo que un modelo chico hace bien: la tarea es acotada, el contexto es
    chico y la salida es una de seis palabras.

    Corre contra Ollama local (no toca la red externa ni ninguna base de datos).
    No modifica nada: solo lee specs/*.md y reporta.

    La salida es un FILTRO, no un veredicto. Medido sobre esta maquina (i5-8250U, sin
    GPU), los modelos de 3-4B aciertan alrededor de la mitad de los casos ambiguos. Por
    eso cada veredicto viene con la linea de evidencia cruda: el modelo prioriza que
    mirar, el humano confirma.

    La seed esta fija (42) para que dos corridas den lo mismo. Correr con -Semilla
    distinta revela que veredictos son estables y cuales dependen del muestreo.

    Uso:
        & .\auditar-specs.ps1
        & .\auditar-specs.ps1 -Modelo gemma3:4b
        & .\auditar-specs.ps1 -Semilla 7
#>

param(
    [string]$Modelo  = 'llama3.2:3b',
    [string]$Ollama  = 'http://localhost:11434',
    [int]$TimeoutSeg = 120,
    [int]$MaxLineas  = 60,
    [int]$MaxChars   = 2500,
    [int]$Semilla    = 42
)

$ErrorActionPreference = 'Stop'

$raiz       = Split-Path -Parent $MyInvocation.MyCommand.Path
$dirSpecs   = Join-Path $raiz 'specs'
$rutaIndice = Join-Path $dirSpecs 'README.md'

if (-not (Test-Path $rutaIndice)) { throw "No encuentro el indice: $rutaIndice" }

# --- Vocabulario cerrado (formato-spec.md §4.1) -------------------------------

$estadosValidos = @('propuesto', 'aprobado', 'implementado', 'en-produccion', 'descartado', 'sin-declarar')

# Se busca por patron y se devuelve el vocabulario canonico, para tolerar que el
# modelo conteste "en producción" (con acento y espacio) o "**implementado**".
# El orden importa: los compuestos primero.
$patrones = @(
    @{ Patron = 'en[ -]produccion'; Valor = 'en-produccion' },
    @{ Patron = 'sin[ -]declarar';  Valor = 'sin-declarar'  },
    @{ Patron = 'descartado';       Valor = 'descartado'    },
    @{ Patron = 'propuesto';        Valor = 'propuesto'     },
    @{ Patron = 'aprobado';         Valor = 'aprobado'      },
    @{ Patron = 'implementado';     Valor = 'implementado'  }
)

function Convertir-Estado {
    param([string]$Texto)

    if ($null -eq $Texto -or $Texto.Trim() -eq '') { return '?' }

    $t = $Texto.Trim().ToLower()
    $t = $t -replace 'ó', 'o'

    foreach ($p in $patrones) {
        if ($t -match $p.Patron) { return $p.Valor }
    }

    return '?'
}

# --- 1. Leer el indice --------------------------------------------------------

$indice = @{}
$ordenIndice = @()

# -Encoding UTF8 explicito: los .md no tienen BOM y Windows PowerShell 5.1 los lee
# con la codificacion ANSI del sistema.
Get-Content $rutaIndice -Encoding UTF8 | ForEach-Object {
    if ($_ -match '^\|\s*\[([^\]]+)\]\(([^)]+\.md)\)\s*\|\s*`?([A-Za-z\-]+)`?\s*\|') {
        $archivo = $matches[2]
        $indice[$archivo] = $matches[3].ToLower()
        $ordenIndice += $archivo
    }
}

Write-Host "Indice: $($indice.Count) filas con archivo."

# --- 2. Extraer el estado declarado en cada spec ------------------------------

$specs = Get-ChildItem $dirSpecs -Filter *.md -File | Where-Object { $_.Name -ne 'README.md' } | Sort-Object Name

Write-Host "Specs a revisar: $($specs.Count)"
Write-Host "Modelo: $Modelo"
Write-Host ''

$resultados = @()
$i = 0

foreach ($spec in $specs) {
    $i++
    Write-Host "[$i/$($specs.Count)] $($spec.Name)" -NoNewline

    $texto = (Get-Content $spec.FullName -TotalCount $MaxLineas -Encoding UTF8) -join "`n"
    if ($texto.Length -gt $MaxChars) { $texto = $texto.Substring(0, $MaxChars) }

    $prompt = @"
Eres un extractor de metadatos. Del documento siguiente responde UNICAMENTE con una de estas palabras, sin puntuacion, sin comillas y sin explicar nada:

propuesto
aprobado
implementado
en-produccion
descartado
sin-declarar

Reglas:
- Busca el estado del PROPIO documento. Puede estar en el frontmatter (una linea "estado: ...") o en un punto que empiece con "**Estado:**".
- Ignora el estado de otros documentos que se mencionen de paso.
- Si el documento no declara su estado, responde: sin-declarar

DOCUMENTO:
$texto
"@

    $declarado = '?'
    $errorModelo = $null

    try {
        $cuerpo = @{
            model   = $Modelo
            prompt  = $prompt
            stream  = $false
            # La seed NO es opcional aca. Sin ella Ollama sortea una por request y la
            # misma entrada da respuestas distintas, incluso con temperature=0: medido
            # sobre esta maquina, un mismo caso repetido 5 veces devolvio 5 respuestas
            # distintas sin seed y 5 identicas con seed=42. Sin esto el informe no es
            # auditable, porque dos corridas del mismo script no dan el mismo resultado.
            #
            # Ojo con el otro filo: una seed fija hace que el mismo ERROR se repita
            # siempre. Reproducible no es lo mismo que correcto. Correr con -Semilla
            # distinta revela que veredictos son estables y cuales son azar del muestreo.
            options = @{ temperature = 0; num_ctx = 4096; seed = $Semilla }
        } | ConvertTo-Json -Depth 5

        # charset=utf-8 no es cosmetico. Windows PowerShell 5.1 codifica el cuerpo de un
        # string segun el ContentType y, sin el charset, manda los acentos mal: Ollama
        # responde 400 y el spec entero se cae. Verificado sobre agente-db.md -- con el
        # charset devuelve "implementado"; sin el, 400.
        $r = Invoke-RestMethod -Uri "$Ollama/api/generate" -Method Post -Body $cuerpo `
                               -ContentType 'application/json; charset=utf-8' -TimeoutSec $TimeoutSeg

        $declarado = Convertir-Estado -Texto $r.response
        Write-Host " -> $declarado"
    }
    catch {
        $errorModelo = $_.Exception.Message
        Write-Host " -> ERROR"
    }

    # Evidencia cruda, para que el humano pueda confirmar el veredicto del modelo
    # sin abrir el archivo.
    # 'Estado:' con dos puntos, no 'Estado' a secas: deploy-azure-aspire.md abre con un
    # texto descriptivo ("Estado del despliegue de Atipico en Azure Container Apps...")
    # que matcheaba primero y tapaba la declaracion real de la linea 6. Con los dos
    # puntos, el frontmatter ("estado: ...") y los puntos ("- **Estado:** ...") siguen
    # entrando, y el texto de prosa queda afuera.
    $lineaEstado = Select-String -Path $spec.FullName -Pattern 'Estado:' -List -Encoding UTF8 | Select-Object -First 1
    $evidencia = if ($lineaEstado) { $lineaEstado.Line.Trim() } else { '(sin linea de Estado)' }
    if ($evidencia.Length -gt 120) { $evidencia = $evidencia.Substring(0, 120) + '...' }

    $delIndice = if ($indice.ContainsKey($spec.Name)) { $indice[$spec.Name] } else { '(sin fila)' }

    $veredicto = if ($errorModelo) { 'ERROR' }
                 elseif ($delIndice -eq '(sin fila)') { 'SIN-FILA' }
                 elseif ($declarado -eq '?') { 'REVISAR' }
                 elseif ($declarado -eq $delIndice) { 'OK' }
                 else { 'DISCREPA' }

    $resultados += [pscustomobject]@{
        Spec      = $spec.Name
        Indice    = $delIndice
        Declarado = $declarado
        Veredicto = $veredicto
        Evidencia = $evidencia
        Error     = $errorModelo
    }
}

# --- 3. Filas del indice que apuntan a archivos inexistentes ------------------

$huerfanas = @()
foreach ($f in $ordenIndice) {
    if (-not (Test-Path (Join-Path $dirSpecs $f))) { $huerfanas += $f }
}

# --- 4. Informe ---------------------------------------------------------------

Write-Host ''
Write-Host '============================================================'
Write-Host ' RESUMEN'
Write-Host '============================================================'

$resumen = $resultados | Group-Object Veredicto | Sort-Object Name
foreach ($g in $resumen) { Write-Host ("  {0,-10} {1}" -f $g.Name, $g.Count) }

$problemas = $resultados | Where-Object { $_.Veredicto -ne 'OK' }

if ($problemas) {
    Write-Host ''
    Write-Host '--- Requieren atencion ---'
    foreach ($p in $problemas) {
        Write-Host ''
        Write-Host ("  {0}  [{1}]" -f $p.Spec, $p.Veredicto)
        Write-Host ("    indice    : {0}" -f $p.Indice)
        Write-Host ("    declarado : {0}" -f $p.Declarado)
        if ($p.Error) { Write-Host ("    error     : {0}" -f $p.Error) }
        Write-Host ("    evidencia : {0}" -f $p.Evidencia)
    }
}
else {
    Write-Host ''
    Write-Host '  Todos los specs coinciden con el indice.'
}

if ($huerfanas) {
    Write-Host ''
    Write-Host '--- Filas del indice sin archivo ---'
    foreach ($h in $huerfanas) { Write-Host "  $h" }
}

Write-Host ''
Write-Host 'Nota: "Declarado" lo produjo un modelo local. La columna Evidencia trae la'
Write-Host 'linea cruda del spec para que puedas confirmar cada DISCREPA antes de tocarlo.'
