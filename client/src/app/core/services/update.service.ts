import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TransferCommand, TransferRequest, TransferStatus } from '../models/update-status.model';

@Injectable({ providedIn: 'root' })
export class UpdateService {
  constructor(private readonly http: HttpClient) {}

  getStatus(): Observable<TransferStatus> {
    return this.http.get<TransferStatus>(`${environment.apiBaseUrl}/status`).pipe(timeout(5000));
  }

  executeCommand(action: TransferCommand, body?: TransferRequest): Observable<void> {
    return this.http.post<void>(`${environment.apiBaseUrl}/${action}`, body ?? {}).pipe(timeout(15000));
  }
}
