import { apiClient } from '../../../api/client';
import { OpportunityDto, UpdateOpportunityStagePayload } from '../types/crm';

export const crmApi = {
  getOpportunities: (): Promise<OpportunityDto[]> => 
    apiClient.get('/api/crm/opportunities'),
    
  updateStage: (payload: UpdateOpportunityStagePayload): Promise<OpportunityDto> =>
    apiClient.put(`/api/crm/opportunities/${payload.id}/stage`, payload),
}
