import { NgModule, provideZoneChangeDetection } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { MainLayoutComponent } from './layouts/main-layout/main-layout.component';

@NgModule({
  declarations: [AppComponent, MainLayoutComponent],
  imports: [BrowserModule, AppRoutingModule],
  providers: [
    provideHttpClient(),
    provideZoneChangeDetection({ eventCoalescing: true })
  ],
  bootstrap: [AppComponent]
})
export class AppModule {}
