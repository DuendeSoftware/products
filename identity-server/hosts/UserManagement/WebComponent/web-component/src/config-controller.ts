import { ReactiveController, ReactiveControllerHost } from 'lit';
import type { AuthConfig } from './types';

export class ConfigController implements ReactiveController {
  private host: ReactiveControllerHost;
  configUrl: string;
  private abortController?: AbortController;
  
  config: AuthConfig | null = null;
  loading = false;
  error: Error | null = null;
  
  constructor(host: ReactiveControllerHost, configUrl: string) {
    this.host = host;
    this.configUrl = configUrl;
    host.addController(this);
  }
  
  hostConnected() {
  }
  
  hostDisconnected() {
    this.abortController?.abort();
  }
  
  async loadConfig() {
    this.loading = true;
    this.error = null;
    this.abortController = new AbortController();
    this.host.requestUpdate();
    
    try {
      const response = await fetch(this.configUrl, {
        headers: {
          'Accept': 'application/json'
        },
        credentials: 'same-origin',
        signal: this.abortController.signal
      });
      
      if (!response.ok) {
        throw new Error(`Failed to load configuration: ${response.statusText}`);
      }
      
      this.config = await response.json();
      this.validateConfig(this.config);
    } catch (e) {
      if (e instanceof Error && e.name === 'AbortError') {
        return;
      }
      this.error = e as Error;
      this.config = null;
    } finally {
      this.loading = false;
      this.host.requestUpdate();
    }
  }
  
  private validateConfig(config: any): asserts config is AuthConfig {
    if (!config || typeof config !== 'object') {
      throw new Error('Invalid configuration format');
    }
    
    if (!Array.isArray(config.primaryAuthenticators)) {
      throw new Error('Configuration must include primaryAuthenticators array');
    }
    
    if (config.primaryAuthenticators.length === 0) {
      throw new Error('At least one primary authentication method must be configured');
    }
    
    if (!Array.isArray(config.secondaryAuthenticators)) {
      throw new Error('Configuration must include secondaryAuthenticators array');
    }
  }
  
  getEnabledPrimaryAuthenticators() {
    return this.config?.primaryAuthenticators.filter(m => m.enabled) || [];
  }
  
  getPrimaryMethodByType(type: string) {
    return this.config?.primaryAuthenticators.find(m => m.type === type && m.enabled);
  }
  
  getSecondaryAuthenticator(type: string) {
    return this.config?.secondaryAuthenticators.find(f => f.type === type);
  }
}
