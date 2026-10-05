export type OpportunityStage = 'Prospecting' | 'Qualification' | 'Proposal' | 'Negotiation' | 'ClosedWon' | 'ClosedLost';

export interface OpportunityDto {
  id: string;
  opportunityNumber: string;
  partyName: string;
  stage: OpportunityStage;
  opportunityAmount: number;
  probability: number;
  weightedAmount: number;
  expectedClosingDate: string;
}

export interface UpdateOpportunityStagePayload {
  id: string;
  newStage: OpportunityStage;
  lossReason?: string;
}
