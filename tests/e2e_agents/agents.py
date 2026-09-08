"""
Agentes simulados deterministas basados en reglas para pruebas de integración A2A.
Totalmente desacoplados de LLM y servicios de terceros.
"""

from __future__ import annotations
from abc import ABC, abstractmethod
from typing import Optional, Union
try:
    from .schemas import (
        JsonRpcRequest,
        NegotiationAction,
        NegotiationMessagePayload,
        ProposalPayload,
        SendMessageParams,
    )
except ImportError:
    from schemas import (
        JsonRpcRequest,
        NegotiationAction,
        NegotiationMessagePayload,
        ProposalPayload,
        SendMessageParams,
    )


class BaseDeterministicAgent(ABC):
    """Clase base para agentes de negociación deterministas basados en reglas."""

    def __init__(
        self,
        agent_id: str,
        initial_price: float,
        reservation_price: float,
        step: float,
        currency: str = "EUR",
        quantity: int = 100,
    ):
        self.agent_id = agent_id
        self.initial_price = float(initial_price)
        self.reservation_price = float(reservation_price)
        self.step = float(step)
        self.currency = currency
        self.quantity = quantity
        self.last_offered_price: Optional[float] = None
        self.history: list[NegotiationMessagePayload] = []

    @property
    def http_headers(self) -> dict[str, str]:
        """Encabezados HTTP requeridos por el Gateway A2A (autenticación perimetral)."""
        return {
            "X-Agent-ID": self.agent_id,
            "Content-Type": "application/json",
        }

    def reset(self) -> None:
        """Reinicia el estado interno del agente para una nueva sesión de negociación."""
        self.last_offered_price = None
        self.history.clear()

    @abstractmethod
    def respond(self, incoming: Optional[NegotiationMessagePayload]) -> NegotiationMessagePayload:
        """Evalúa el mensaje recibido y genera la siguiente acción determinista."""
        pass

    def build_send_message_request(
        self,
        negotiation_id: str,
        turn_index: int,
        payload: NegotiationMessagePayload,
        request_id: Optional[Union[str, int]] = None,
    ) -> JsonRpcRequest[SendMessageParams]:
        """Construye una petición JSON-RPC 2.0 conforme a la Fase 2 y 4 del Gateway."""
        params = SendMessageParams(
            negotiation_id=negotiation_id,
            turn_index=turn_index,
            sender_id=self.agent_id,
            message=payload,
        )
        return JsonRpcRequest[SendMessageParams](
            method="send_message",
            params=params,
            id=request_id or f"{self.agent_id}-turn-{turn_index}",
        )


class BuyerAgent(BaseDeterministicAgent):
    """
    Agente Comprador determinista.
    - Estrategia: Comienza ofertando un precio bajo. Incrementa su oferta en pasos fijos.
    - Techo de reserva (reservation_price): Precio máximo que está dispuesto a pagar.
    - Convergencia: Acepta si la oferta recibida es menor o igual a su techo de reserva.
    """

    def __init__(
        self,
        agent_id: str = "urn:agent:buyer",
        initial_price: float = 75.0,
        reservation_price: float = 95.0,
        step: float = 5.0,
        currency: str = "EUR",
        quantity: int = 100,
    ):
        super().__init__(
            agent_id=agent_id,
            initial_price=initial_price,
            reservation_price=reservation_price,
            step=step,
            currency=currency,
            quantity=quantity,
        )

    def respond(self, incoming: Optional[NegotiationMessagePayload]) -> NegotiationMessagePayload:
        if incoming is None:
            # Turno inicial: Oferta base
            self.last_offered_price = self.initial_price
            msg = NegotiationMessagePayload(
                action=NegotiationAction.OFFER,
                proposal=ProposalPayload(
                    unit_price=self.initial_price,
                    currency=self.currency,
                    quantity=self.quantity,
                    delivery_terms="DAP",
                    payment_terms_days=30,
                ),
                text=f"Initial purchase offer of {self.initial_price} {self.currency} by {self.agent_id}.",
            )
            self.history.append(msg)
            return msg

        # Registrar propuesta recibida
        self.history.append(incoming)

        if incoming.action in (NegotiationAction.ACCEPT, NegotiationAction.REJECT, NegotiationAction.WITHDRAW):
            return incoming

        incoming_price = incoming.proposal.unit_price if incoming.proposal else 0.0

        # Criterio de convergencia: ¿La oferta de la contraparte está dentro del techo de reserva?
        if incoming_price <= self.reservation_price:
            self.last_offered_price = incoming_price
            accept_msg = NegotiationMessagePayload(
                action=NegotiationAction.ACCEPT,
                proposal=incoming.proposal,
                text=f"Buyer {self.agent_id} accepts counter-offer of {incoming_price} {self.currency} (ceiling: {self.reservation_price}).",
            )
            self.history.append(accept_msg)
            return accept_msg

        # Generar contraoferta incrementada
        base_price = self.last_offered_price if self.last_offered_price is not None else self.initial_price
        new_price = min(base_price + self.step, self.reservation_price)

        self.last_offered_price = new_price
        counter_msg = NegotiationMessagePayload(
            action=NegotiationAction.COUNTER_OFFER,
            proposal=ProposalPayload(
                unit_price=new_price,
                currency=self.currency,
                quantity=self.quantity,
                delivery_terms="DAP",
                payment_terms_days=30,
            ),
            text=f"Buyer {self.agent_id} counter-offers {new_price} {self.currency}.",
        )
        self.history.append(counter_msg)
        return counter_msg


class SellerAgent(BaseDeterministicAgent):
    """
    Agente Vendedor determinista.
    - Estrategia: Comienza pidiendo un precio alto. Reduce su demanda en pasos fijos.
    - Suelo de reserva (reservation_price): Precio mínimo por debajo del cual no vende.
    - Convergencia: Acepta si la oferta recibida es mayor o igual a su suelo de reserva.
    """

    def __init__(
        self,
        agent_id: str = "urn:agent:seller",
        initial_price: float = 105.0,
        reservation_price: float = 85.0,
        step: float = 5.0,
        currency: str = "EUR",
        quantity: int = 100,
    ):
        super().__init__(
            agent_id=agent_id,
            initial_price=initial_price,
            reservation_price=reservation_price,
            step=step,
            currency=currency,
            quantity=quantity,
        )

    def respond(self, incoming: Optional[NegotiationMessagePayload]) -> NegotiationMessagePayload:
        if incoming is None:
            # Oferta inicial de venta
            self.last_offered_price = self.initial_price
            msg = NegotiationMessagePayload(
                action=NegotiationAction.OFFER,
                proposal=ProposalPayload(
                    unit_price=self.initial_price,
                    currency=self.currency,
                    quantity=self.quantity,
                    delivery_terms="DAP",
                    payment_terms_days=30,
                ),
                text=f"Initial sale offer of {self.initial_price} {self.currency} by {self.agent_id}.",
            )
            self.history.append(msg)
            return msg

        # Registrar propuesta recibida
        self.history.append(incoming)

        if incoming.action in (NegotiationAction.ACCEPT, NegotiationAction.REJECT, NegotiationAction.WITHDRAW):
            return incoming

        incoming_price = incoming.proposal.unit_price if incoming.proposal else 0.0

        # Criterio de convergencia: ¿La oferta de la contraparte está por encima del suelo de reserva?
        if incoming_price >= self.reservation_price:
            self.last_offered_price = incoming_price
            accept_msg = NegotiationMessagePayload(
                action=NegotiationAction.ACCEPT,
                proposal=incoming.proposal,
                text=f"Seller {self.agent_id} accepts offer of {incoming_price} {self.currency} (floor: {self.reservation_price}).",
            )
            self.history.append(accept_msg)
            return accept_msg

        # Generar contraoferta decrementada
        base_price = self.last_offered_price if self.last_offered_price is not None else self.initial_price
        new_price = max(base_price - self.step, self.reservation_price)

        self.last_offered_price = new_price
        counter_msg = NegotiationMessagePayload(
            action=NegotiationAction.COUNTER_OFFER,
            proposal=ProposalPayload(
                unit_price=new_price,
                currency=self.currency,
                quantity=self.quantity,
                delivery_terms="DAP",
                payment_terms_days=30,
            ),
            text=f"Seller {self.agent_id} counter-offers {new_price} {self.currency}.",
        )
        self.history.append(counter_msg)
        return counter_msg
