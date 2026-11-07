import { Component, OnDestroy, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { EMPTY, Subject, catchError, exhaustMap, finalize, takeUntil, timer } from 'rxjs';
import { TransferCommand, TransferFile, TransferRequest, TransferStatus } from '../../core/models/update-status.model';
import { UpdateService } from '../../core/services/update.service';

@Component({
  selector: 'app-home',
  standalone: false,
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit, OnDestroy {
  status: TransferStatus = {
    phase: 'API bekleniyor', error: null, sourceFileCount: 0, destinationFileCount: 0,
    delayMilliseconds: 150, skippedFiles: 0, plannedFiles: 0,
    completedFiles: 0, remainingFiles: 0, totalBytes: 0, transferredBytes: 0, files: []
  };
  isConnected = false;
  isCommandPending = false;
  connectionError = '';
  actionError = '';
  isSlow = true;
  filter = 'all';
  private readonly destroy$ = new Subject<void>();

  constructor(private readonly updateService: UpdateService) {}

  get isRunning(): boolean {
    return ['Aktarılıyor', 'Yeniden deneniyor', 'Doğrulanıyor'].includes(this.status.phase);
  }

  get visibleFiles(): TransferFile[] {
    return this.status.files.filter(file => this.filter === 'all'
      || (this.filter === 'same' && file.action === 'Atla')
      || (this.filter === 'pending' && file.action !== 'Atla' && file.phase !== 'Tamamlandı')
      || (this.filter === 'completed' && file.phase === 'Tamamlandı'));
  }

  ngOnInit(): void {
    timer(0, 500).pipe(
      exhaustMap(() => this.updateService.getStatus().pipe(catchError(() => {
        this.isConnected = false;
        this.connectionError = 'API bağlantısı kurulamadı. .NET projesinin çalıştığını kontrol edin.';
        return EMPTY;
      }))),
      takeUntil(this.destroy$)
    ).subscribe(status => {
      this.status = status;
      this.isSlow = status.delayMilliseconds !== 0;
      this.isConnected = true;
      this.connectionError = '';
    });
  }

  executeCommand(action: TransferCommand, body?: TransferRequest): void {
    if (this.isCommandPending || !this.isConnected) return;
    this.actionError = '';
    this.isCommandPending = true;
    this.updateService.executeCommand(action, body).pipe(
      takeUntil(this.destroy$),
      finalize(() => this.isCommandPending = false)
    ).subscribe({
      error: (error: unknown) => {
        this.actionError = error instanceof HttpErrorResponse
          ? error.error?.message || (typeof error.error === 'string' ? error.error : 'İşlem tamamlanamadı.')
          : 'İşlem tamamlanamadı.';
      }
    });
  }

  start(): void { this.executeCommand('start', { isSlow: this.isSlow }); }

  changeSpeed(event: Event): void {
    this.isSlow = (event.target as HTMLSelectElement).value === 'true';
    this.executeCommand('speed', { isSlow: this.isSlow });
  }

  changeFilter(event: Event): void { this.filter = (event.target as HTMLSelectElement).value; }


  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }
}
