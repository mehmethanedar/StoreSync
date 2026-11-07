import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-progress-bar',
  standalone: false,
  templateUrl: './progress-bar.component.html',
  styleUrls: ['./progress-bar.component.scss']
})
export class ProgressBarComponent {
  @Input() value = 0;
  @Input() total = 0;
}
