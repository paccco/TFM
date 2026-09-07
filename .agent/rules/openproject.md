# Protocolo de Integración con OpenProject CLI (`scripts/op_cli.py`)

Este documento define el protocolo estricto y secuencial que el Agente IA debe seguir para interactuar con la instancia de **OpenProject API v3** a través del script CLI local `scripts/op_cli.py`.

---

## 1. Variables de Entorno Requeridas

Antes de invocar cualquier comando, deben estar disponibles en el entorno de ejecución:

| Variable | Requerida | Descripción | Ejemplo |
| :--- | :---: | :--- | :--- |
| `OPENPROJECT_URL` | Sí | URL base de la instancia OpenProject | `http://localhost:8080` |
| `OPENPROJECT_API_KEY` | Sí | Token API de usuario generado en OpenProject | `9a8b7c6d5e4f...` |
| `OP_CF_TOKENS` | Opcional | ID numérico del Custom Field para registrar tokens | `1` |
| `OP_CF_MODEL` | Opcional | ID numérico del Custom Field para registrar el modelo | `2` |

---

## 2. Ciclo de Vida de una Tarea (Work Package)

Toda tarea asignada a un agente o desarrollador debe atravesar cuatro fases operativas estrictas:

```text
[Descubrimiento / Verificación]
               │
               ▼
   [Inicio de Tarea: in_progress] ───> Creación de rama feature/OP-<ID>-<slug>
               │
               ▼
[Trazabilidad en Commits: comment] ──> Cada commit relevante registrado con su hash
               │
               ▼
  [Cierre y Métricas: closed] ─────> Registro de tokens consumidos, modelo y merge
```

---

## 3. Protocolo de Ejecución de Comandos

### Paso 1: Autodescubrimiento y Verificación Inicial
Si el agente desconoce los IDs de tipos, estados o custom fields de la instancia local, ejecuta:
```bash
python3 scripts/op_cli.py discover
```

Para verificar el estado actual, título y descripción técnica de un Work Package asignado:
```bash
python3 scripts/op_cli.py get --id <ID>
```

### Paso 2: Transición a Estado "En Progreso" (`in_progress`)
Al comenzar a trabajar en un Work Package (e instanciar la rama Git correspondiente):
```bash
python3 scripts/op_cli.py update \
  --id <ID> \
  --status in_progress \
  --comment "Iniciando rama feature/OP-<ID>-<slug>"
```

### Paso 3: Trazabilidad Continua de Commits (`comment`)
Cada vez que se efectúe un commit en la rama de trabajo, se debe enlazar en el historial del paquete de trabajo indicando el hash y una breve descripción:
```bash
python3 scripts/op_cli.py comment \
  --id <ID> \
  --text "Commit <HASH>: <descripción del cambio técnico>"
```
*Ejemplo:*
```bash
python3 scripts/op_cli.py comment \
  --id 14 \
  --text "Commit 7f3a9b1: feat(fsm): add transition guard for margin limits"
```

### Paso 4: Cierre con Registro de Métricas (`closed`)
Tras fusionar la rama de trabajo en `develop`, ejecutar las pruebas unitarias con éxito y compilar la documentación técnica LaTeX (`docs/`):
```bash
python3 scripts/op_cli.py update \
  --id <ID> \
  --status closed \
  --tokens <NUMERO_TOKENS> \
  --model "<NOMBRE_MODELO>" \
  --comment "Implementación terminada y documentación LaTeX compilada."
```
*Ejemplo:*
```bash
python3 scripts/op_cli.py update \
  --id 14 \
  --status closed \
  --tokens 18450 \
  --model "gemini-3.8-flash" \
  --comment "Implementación terminada y documentación LaTeX compilada."
```

---

## 4. Creación de Tareas Secundarias o Sub-paquetes
Si durante la planificación se detecta la necesidad de crear un nuevo Work Package técnico:
```bash
python3 scripts/op_cli.py create \
  --project "<PROJECT_ID_OR_SLUG>" \
  --subject "<Título de la tarea>" \
  --desc "<Especificación técnica detallada>" \
  --type "task" \
  --parent <ID_PADRE>
```
