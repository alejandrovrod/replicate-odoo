using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace AssetHub.Application.Billing.Commands;

public record ProcessStripeWebhookCommand(string Payload, string Signature) : IRequest<bool>;

public class ProcessStripeWebhookCommandHandler : IRequestHandler<ProcessStripeWebhookCommand, bool>
{
    public Task<bool> Handle(ProcessStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        // TODO: Validar firma de Stripe
        // TODO: Procesar el evento (invoice.paid, invoice.payment_failed, customer.subscription.deleted)
        // TODO: Actualizar Subscription.Status y Tenant.Status
        
        return Task.FromResult(true);
    }
}
