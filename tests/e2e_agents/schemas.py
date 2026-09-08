"""
Esquemas Pydantic para el protocolo de comunicación A2A sobre JSON-RPC 2.0.
Conformes a la especificación del A2A Gateway (.NET 9 / ASP.NET Core) y su pipeline
de validación en 4 fases.
"""

from __future__ import annotations
from enum import Enum
from typing import Any, Generic, Optional, TypeVar, Union
from pydantic import BaseModel, ConfigDict, Field


TParams = TypeVar("TParams")
TResult = TypeVar("TResult")


class NegotiationAction(str, Enum):
    """Acciones normativas en el protocolo de negociación A2A."""
    OFFER = "OFFER"
    COUNTER_OFFER = "COUNTER_OFFER"
    ACCEPT = "ACCEPT"
    REJECT = "REJECT"
    WITHDRAW = "WITHDRAW"


class ProposalPayload(BaseModel):
    """Detalle comercial de una propuesta u oferta de negociación."""
    unit_price: float = Field(..., description="Precio unitario negociado")
    currency: str = Field(default="EUR", description="Código de divisa ISO 4217")
    quantity: int = Field(default=100, description="Cantidad de unidades")
    delivery_terms: str = Field(default="DAP", description="Término internacional de comercio (Incoterm)")
    payment_terms_days: int = Field(default=30, description="Plazo de pago en días")

    model_config = ConfigDict(populate_by_name=True)


class NegotiationMessagePayload(BaseModel):
    """Estructura interna del mensaje de negociación intercambiado entre agentes."""
    action: NegotiationAction = Field(..., description="Acción del turno actual")
    proposal: Optional[ProposalPayload] = Field(default=None, description="Propuesta comercial asociada")
    text: Optional[str] = Field(default=None, description="Comentario o justificación textual opcional")

    model_config = ConfigDict(populate_by_name=True)


class SendMessageParams(BaseModel):
    """Parámetros requeridos para el método JSON-RPC 'send_message' (Fase 2 y 4)."""
    negotiation_id: str = Field(..., description="Identificador único de la sesión de negociación")
    turn_index: int = Field(..., ge=0, description="Índice de turno monótono creciente (entero >= 0)")
    sender_id: str = Field(..., description="Identificador URI del agente emisor (debe coincidir con X-Agent-ID)")
    message: Union[NegotiationMessagePayload, dict[str, Any], str] = Field(
        ..., description="Carga útil del mensaje estructurado o texto"
    )

    model_config = ConfigDict(populate_by_name=True)


class CreateTaskParams(BaseModel):
    """Parámetros para inicializar una tarea o negociación vía 'create_task'."""
    tenant_id: str = Field(..., description="Identificador del tenant solicitante")
    skill: str = Field(..., description="Nombre de la habilidad declarada en la Agent Card")
    negotiation_id: Optional[str] = Field(default=None, description="Identificador opcional preasignado")

    model_config = ConfigDict(populate_by_name=True)


class NegotiationDispatchResult(BaseModel):
    """Esquema de respuesta de éxito emitido por el Gateway al despachar una orden."""
    success: bool = Field(..., description="Indica si el despacho fue exitoso")
    negotiation_id: str = Field(..., alias="negotiationId", description="Identificador de la negociación")
    turn_index: int = Field(..., alias="turnIndex", description="Índice de turno registrado")
    status: str = Field(..., description="Estado devuelto por el perímetro ('acknowledged')")
    output: Optional[Any] = Field(default=None, description="Echo o payload transformado")
    message: Optional[str] = Field(default=None, description="Mensaje descriptivo del despacho")

    model_config = ConfigDict(populate_by_name=True)


class JsonRpcRequest(BaseModel, Generic[TParams]):
    """Estructura canónica de petición JSON-RPC 2.0."""
    jsonrpc: str = Field(default="2.0", description="Versión fija del protocolo JSON-RPC")
    method: str = Field(..., description="Nombre del método o skill invocada")
    params: TParams = Field(..., description="Parámetros estructurados de la invocación")
    id: Optional[Union[str, int]] = Field(default=None, description="Identificador de correlación")

    model_config = ConfigDict(populate_by_name=True)


class JsonRpcError(BaseModel):
    """Estructura de error estándar JSON-RPC 2.0."""
    code: int = Field(..., description="Código numérico de error (-32700, -32600, -32601, -32602, -32001, etc.)")
    message: str = Field(..., description="Mensaje descriptivo del error")
    data: Optional[Any] = Field(default=None, description="Información de diagnóstico o detalles adicionales")

    model_config = ConfigDict(populate_by_name=True)


class JsonRpcResponse(BaseModel, Generic[TResult]):
    """Estructura canónica de respuesta JSON-RPC 2.0 (éxito o error)."""
    jsonrpc: str = Field(default="2.0", description="Versión fija del protocolo JSON-RPC")
    result: Optional[TResult] = Field(default=None, description="Resultado fuertemente tipado en caso de éxito")
    error: Optional[JsonRpcError] = Field(default=None, description="Objeto de error si la petición falló")
    id: Optional[Union[str, int]] = Field(default=None, description="Identificador correlativo de la petición")

    model_config = ConfigDict(populate_by_name=True)

    @property
    def is_success(self) -> bool:
        return self.error is None

    @property
    def is_error(self) -> bool:
        return self.error is not None
