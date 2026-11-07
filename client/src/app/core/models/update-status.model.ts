export interface TransferFile {
  name: string;
  size: number;
  sha256: string;
  action: 'Atla' | 'Yeni' | 'Güncelle';
  phase: string;
  bytes: number;
}

export interface TransferStatus {
  phase: string;
  error: string | null;
  sourceFileCount: number;
  destinationFileCount: number;
  delayMilliseconds: number;
  skippedFiles: number;
  plannedFiles: number;
  completedFiles: number;
  remainingFiles: number;
  totalBytes: number;
  transferredBytes: number;
  files: TransferFile[];
}

export type TransferCommand = 'analyze' | 'start' | 'pause' | 'speed' | 'reset';
export interface TransferRequest { isSlow: boolean; }
