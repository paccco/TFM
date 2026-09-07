#!/usr/bin/env python3
"""
scripts/op_cli.py
Herramienta de integración local entre el Agente IA y OpenProject v3 API.

Variables de entorno requeridas:
  - OPENPROJECT_URL: URL base (ej. http://localhost:8080)
  - OPENPROJECT_API_KEY: Token de usuario generado en OpenProject

Variables de entorno opcionales (Custom Fields en OpenProject):
  - OP_CF_TOKENS: ID numérico del campo personalizado para registrar tokens
  - OP_CF_MODEL: ID numérico del campo personalizado para registrar el modelo usado
"""

import os
import sys
import json
import argparse
import requests
from requests.auth import HTTPBasicAuth

# Cargar variables desde .env si existe
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass

# --- Configuración Base ---
OPENPROJECT_URL = os.getenv("OPENPROJECT_URL", "http://localhost:8080").rstrip("/")
API_KEY = os.getenv("OPENPROJECT_API_KEY", "")

# Mapeos estándar de IDs de OpenProject (Ajustar según tu configuración local)
STATUS_MAP = {
    "new": 1,
    "in_progress": 7,
    "closed": 12,
    "rejected": 13,
}

TYPE_MAP = {
    "task": 1,
    "feature": 2,
    "bug": 3,
}


def _get_auth():
    """Genera la autenticación HTTP Basic requerida por la API v3."""
    if not API_KEY:
        sys.exit("ERROR: Variable OPENPROJECT_API_KEY no definida en el entorno.")
    return HTTPBasicAuth("apikey", API_KEY)


def _handle_response(response):
    """Manejo centralizado de respuestas HTTP de OpenProject."""
    try:
        response.raise_for_status()
        if response.status_code == 204:
            return {}
        return response.json()
    except requests.exceptions.HTTPError:
        print(f"HTTP_ERROR {response.status_code}: {response.text}", file=sys.stderr)
        sys.exit(1)


# --- Operaciones de Lectura y Autodescubrimiento ---

def get_task(task_id: int):
    """Obtiene los detalles clave de una tarea existente."""
    url = f"{OPENPROJECT_URL}/api/v3/work_packages/{task_id}"
    r = requests.get(url, auth=_get_auth())
    wp = _handle_response(r)

    summary = {
        "id": wp["id"],
        "subject": wp["subject"],
        "status": wp["_links"]["status"]["title"],
        "type": wp["_links"]["type"]["title"],
        "lockVersion": wp["lockVersion"],
        "description": wp.get("description", {}).get("raw", ""),
        "parent": wp["_links"].get("parent", {}).get("title"),
        "version": wp["_links"].get("version", {}).get("title"),
    }
    print(json.dumps(summary, indent=2, ensure_ascii=False))


def discover_schema():
    """Permite al agente listar tipos, estados y custom fields de la instancia."""
    auth = _get_auth()
    endpoints = {
        "projects": f"{OPENPROJECT_URL}/api/v3/projects",
        "types": f"{OPENPROJECT_URL}/api/v3/types",
        "statuses": f"{OPENPROJECT_URL}/api/v3/statuses",
        "custom_fields": f"{OPENPROJECT_URL}/api/v3/custom_fields",
    }
    discovery = {}
    for key, url in endpoints.items():
        r = requests.get(url, auth=auth)
        if r.status_code == 404:
            discovery[key] = []
            continue
        data = _handle_response(r)
        items = data.get("_embedded", {}).get("elements", [])
        if key == "projects":
            discovery[key] = [{"id": item["id"], "identifier": item.get("identifier"), "name": item["name"]} for item in items]
        else:
            discovery[key] = [{"id": item["id"], "name": item["name"]} for item in items]

    print(json.dumps(discovery, indent=2, ensure_ascii=False))


# --- Operaciones de Escritura ---

def create_task(
    project_id: str,
    subject: str,
    description: str,
    task_type: str = "task",
    parent_id: int | None = None,
    version_id: int | None = None,
):
    """Crea un paquete de trabajo (work package) con especificaciones técnicas."""
    url = f"{OPENPROJECT_URL}/api/v3/projects/{project_id}/work_packages"
    type_id = TYPE_MAP.get(task_type, 1)

    payload = {
        "subject": subject,
        "description": {"raw": description},
        "_links": {
            "type": {"href": f"/api/v3/types/{type_id}"}
        },
    }

    if parent_id:
        payload["_links"]["parent"] = {"href": f"/api/v3/work_packages/{parent_id}"}
    if version_id:
        payload["_links"]["version"] = {"href": f"/api/v3/versions/{version_id}"}

    r = requests.post(url, json=payload, auth=_get_auth())
    wp = _handle_response(r)
    print(f"TASK_CREATED: #{wp['id']} - {wp['subject']}")


def update_task(
    task_id: int,
    status: str | None = None,
    tokens: int | None = None,
    model_name: str | None = None,
    comment: str | None = None,
    cf_tokens_id: str | None = None,
    cf_model_id: str | None = None,
):
    """Actualiza estado, custom fields y añade traza de desarrollo a la tarea."""
    auth = _get_auth()

    # 1. Obtención de lockVersion requerida por concurrencia optimista
    get_url = f"{OPENPROJECT_URL}/api/v3/work_packages/{task_id}"
    res = _handle_response(requests.get(get_url, auth=auth))
    payload = {"lockVersion": res["lockVersion"]}

    # 2. Cambio de estado
    if status and status in STATUS_MAP:
        payload["_links"] = payload.get("_links", {})
        payload["_links"]["status"] = {"href": f"/api/v3/statuses/{STATUS_MAP[status]}"}

    # 3. Registro de métricas IA (vía Custom Field o fallback a comentario)
    cf_tokens_id = cf_tokens_id or os.getenv("OP_CF_TOKENS")
    cf_model_id = cf_model_id or os.getenv("OP_CF_MODEL")
    extra_audit = []

    if tokens is not None:
        if cf_tokens_id:
            payload[f"customField_{cf_tokens_id}"] = int(tokens)
        else:
            extra_audit.append(f"Tokens consumidos: {tokens}")

    if model_name:
        if cf_model_id:
            payload[f"customField_{cf_model_id}"] = model_name
        else:
            extra_audit.append(f"Modelo IA: {model_name}")

    patch_url = f"{OPENPROJECT_URL}/api/v3/work_packages/{task_id}"
    r = requests.patch(patch_url, json=payload, auth=auth)
    _handle_response(r)

    # 4. Inserción de notas, commits y métricas en el historial
    full_comment = comment or ""
    if extra_audit:
        metrics_block = "\n\n**Métricas de Sesión IA:**\n- " + "\n- ".join(extra_audit)
        full_comment = (full_comment + metrics_block).strip()

    if full_comment:
        comment_url = f"{OPENPROJECT_URL}/api/v3/work_packages/{task_id}/activities"
        c_payload = {"comment": {"raw": full_comment}}
        r_c = requests.post(comment_url, json=c_payload, auth=auth)
        _handle_response(r_c)

    print(f"TASK_UPDATED: #{task_id}")


def comment_task(task_id: int, text: str):
    """Añade un comentario simple o traza de git al historial de la tarea."""
    url = f"{OPENPROJECT_URL}/api/v3/work_packages/{task_id}/activities"
    payload = {"comment": {"raw": text}}
    r = requests.post(url, json=payload, auth=_get_auth())
    _handle_response(r)
    print(f"COMMENT_ADDED: #{task_id}")


# --- Configuración CLI ---

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="CLI de soporte para desarrollo con Agente IA en OpenProject")
    subparsers = parser.add_subparsers(dest="command")

    # discover
    subparsers.add_parser("discover", help="Descubre IDs de tipos, estados y custom fields")

    # get
    get_p = subparsers.add_parser("get", help="Obtiene la información de una tarea")
    get_p.add_argument("--id", type=int, required=True, help="ID de la tarea")

    # create
    create_p = subparsers.add_parser("create", help="Crea una nueva tarea")
    create_p.add_argument("--project", required=True, help="ID o slug del proyecto")
    create_p.add_argument("--subject", required=True, help="Título de la tarea")
    create_p.add_argument("--desc", required=True, help="Descripción técnica")
    create_p.add_argument("--type", default="task", choices=["task", "feature", "bug"])
    create_p.add_argument("--parent", type=int, default=None, help="ID de tarea padre")
    create_p.add_argument("--version", type=int, default=None, help="ID de versión/hito")

    # update
    update_p = subparsers.add_parser("update", help="Actualiza estado, métricas de tokens y añade trazas")
    update_p.add_argument("--id", type=int, required=True, help="ID de la tarea")
    update_p.add_argument("--status", choices=["new", "in_progress", "closed", "rejected"])
    update_p.add_argument("--tokens", type=int, default=None, help="Total de tokens consumidos")
    update_p.add_argument("--model", default=None, help="Identificador del modelo (ej. claude-3-5-sonnet)")
    update_p.add_argument("--comment", default=None, help="Texto o commit para el historial")
    update_p.add_argument("--cf-tokens-id", default=None, help="ID de customField para tokens")
    update_p.add_argument("--cf-model-id", default=None, help="ID de customField para modelo")

    # comment
    comment_p = subparsers.add_parser("comment", help="Añade un comentario")
    comment_p.add_argument("--id", type=int, required=True, help="ID de la tarea")
    comment_p.add_argument("--text", required=True, help="Contenido del comentario")

    args = parser.parse_args()

    if args.command == "discover":
        discover_schema()
    elif args.command == "get":
        get_task(args.id)
    elif args.command == "create":
        create_task(args.project, args.subject, args.desc, args.type, args.parent, args.version)
    elif args.command == "update":
        update_task(
            task_id=args.id,
            status=args.status,
            tokens=args.tokens,
            model_name=args.model,
            comment=args.comment,
            cf_tokens_id=args.cf_tokens_id,
            cf_model_id=args.cf_model_id,
        )
    elif args.command == "comment":
        comment_task(args.id, args.text)
    else:
        parser.print_help()
