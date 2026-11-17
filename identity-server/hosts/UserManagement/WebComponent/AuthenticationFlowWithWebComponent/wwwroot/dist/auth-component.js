var t=globalThis,e=t.ShadowRoot&&(void 0===t.ShadyCSS||t.ShadyCSS.nativeShadow)&&"adoptedStyleSheets"in Document.prototype&&"replace"in CSSStyleSheet.prototype,r=Symbol(),i=/* @__PURE__ */new WeakMap,o=class{constructor(t,e,i){if(this._$cssResult$=!0,i!==r)throw Error("CSSResult is not constructable. Use `unsafeCSS` or `css` instead.");this.cssText=t,this.t=e}get styleSheet(){let t=this.o;const r=this.t;if(e&&void 0===t){const e=void 0!==r&&1===r.length;e&&(t=i.get(r)),void 0===t&&((this.o=t=new CSSStyleSheet).replaceSync(this.cssText),e&&i.set(r,t))}return t}toString(){return this.cssText}},a=(t,...e)=>{const i=1===t.length?t[0]:e.reduce((e,r,i)=>e+(t=>{if(!0===t._$cssResult$)return t.cssText;if("number"==typeof t)return t;throw Error("Value passed to 'css' function must be a 'css' function result: "+t+". Use 'unsafeCSS' to pass non-literal values, but take care to ensure page security.")})(r)+t[i+1],t[0]);return new o(i,t,r)},s=e?t=>t:t=>t instanceof CSSStyleSheet?(t=>{let e="";for(const r of t.cssRules)e+=r.cssText;return(t=>new o("string"==typeof t?t:t+"",void 0,r))(e)})(t):t,{is:n,defineProperty:h,getOwnPropertyDescriptor:d,getOwnPropertyNames:l,getOwnPropertySymbols:c,getPrototypeOf:u}=Object,p=globalThis,m=p.trustedTypes,f=m?m.emptyScript:"",v=p.reactiveElementPolyfillSupport,g=(t,e)=>t,y={toAttribute(t,e){switch(e){case Boolean:t=t?f:null;break;case Object:case Array:t=null==t?t:JSON.stringify(t)}return t},fromAttribute(t,e){let r=t;switch(e){case Boolean:r=null!==t;break;case Number:r=null===t?null:Number(t);break;case Object:case Array:try{r=JSON.parse(t)}catch(t){r=null}}return r}},b=(t,e)=>!n(t,e),$={attribute:!0,type:String,converter:y,reflect:!1,useDefault:!1,hasChanged:b};Symbol.metadata??=Symbol("metadata"),p.litPropertyMetadata??=/* @__PURE__ */new WeakMap;var _=class extends HTMLElement{static addInitializer(t){this._$Ei(),(this.l??=[]).push(t)}static get observedAttributes(){return this.finalize(),this._$Eh&&[...this._$Eh.keys()]}static createProperty(t,e=$){if(e.state&&(e.attribute=!1),this._$Ei(),this.prototype.hasOwnProperty(t)&&((e=Object.create(e)).wrapped=!0),this.elementProperties.set(t,e),!e.noAccessor){const r=Symbol(),i=this.getPropertyDescriptor(t,r,e);void 0!==i&&h(this.prototype,t,i)}}static getPropertyDescriptor(t,e,r){const{get:i,set:o}=d(this.prototype,t)??{get(){return this[e]},set(t){this[e]=t}};return{get:i,set(e){const a=i?.call(this);o?.call(this,e),this.requestUpdate(t,a,r)},configurable:!0,enumerable:!0}}static getPropertyOptions(t){return this.elementProperties.get(t)??$}static _$Ei(){if(this.hasOwnProperty(g("elementProperties")))return;const t=u(this);t.finalize(),void 0!==t.l&&(this.l=[...t.l]),this.elementProperties=new Map(t.elementProperties)}static finalize(){if(this.hasOwnProperty(g("finalized")))return;if(this.finalized=!0,this._$Ei(),this.hasOwnProperty(g("properties"))){const t=this.properties,e=[...l(t),...c(t)];for(const r of e)this.createProperty(r,t[r])}const t=this[Symbol.metadata];if(null!==t){const e=litPropertyMetadata.get(t);if(void 0!==e)for(const[t,r]of e)this.elementProperties.set(t,r)}this._$Eh=/* @__PURE__ */new Map;for(const[e,r]of this.elementProperties){const t=this._$Eu(e,r);void 0!==t&&this._$Eh.set(t,e)}this.elementStyles=this.finalizeStyles(this.styles)}static finalizeStyles(t){const e=[];if(Array.isArray(t)){const r=new Set(t.flat(1/0).reverse());for(const t of r)e.unshift(s(t))}else void 0!==t&&e.push(s(t));return e}static _$Eu(t,e){const r=e.attribute;return!1===r?void 0:"string"==typeof r?r:"string"==typeof t?t.toLowerCase():void 0}constructor(){super(),this._$Ep=void 0,this.isUpdatePending=!1,this.hasUpdated=!1,this._$Em=null,this._$Ev()}_$Ev(){this._$ES=new Promise(t=>this.enableUpdating=t),this._$AL=/* @__PURE__ */new Map,this._$E_(),this.requestUpdate(),this.constructor.l?.forEach(t=>t(this))}addController(t){(this._$EO??=/* @__PURE__ */new Set).add(t),void 0!==this.renderRoot&&this.isConnected&&t.hostConnected?.()}removeController(t){this._$EO?.delete(t)}_$E_(){const t=/* @__PURE__ */new Map,e=this.constructor.elementProperties;for(const r of e.keys())this.hasOwnProperty(r)&&(t.set(r,this[r]),delete this[r]);t.size>0&&(this._$Ep=t)}createRenderRoot(){const r=this.shadowRoot??this.attachShadow(this.constructor.shadowRootOptions);return((r,i)=>{if(e)r.adoptedStyleSheets=i.map(t=>t instanceof CSSStyleSheet?t:t.styleSheet);else for(const e of i){const i=document.createElement("style"),o=t.litNonce;void 0!==o&&i.setAttribute("nonce",o),i.textContent=e.cssText,r.appendChild(i)}})(r,this.constructor.elementStyles),r}connectedCallback(){this.renderRoot??=this.createRenderRoot(),this.enableUpdating(!0),this._$EO?.forEach(t=>t.hostConnected?.())}enableUpdating(t){}disconnectedCallback(){this._$EO?.forEach(t=>t.hostDisconnected?.())}attributeChangedCallback(t,e,r){this._$AK(t,r)}_$ET(t,e){const r=this.constructor.elementProperties.get(t),i=this.constructor._$Eu(t,r);if(void 0!==i&&!0===r.reflect){const o=(void 0!==r.converter?.toAttribute?r.converter:y).toAttribute(e,r.type);this._$Em=t,null==o?this.removeAttribute(i):this.setAttribute(i,o),this._$Em=null}}_$AK(t,e){const r=this.constructor,i=r._$Eh.get(t);if(void 0!==i&&this._$Em!==i){const t=r.getPropertyOptions(i),o="function"==typeof t.converter?{fromAttribute:t.converter}:void 0!==t.converter?.fromAttribute?t.converter:y;this._$Em=i;const a=o.fromAttribute(e,t.type);this[i]=a??this._$Ej?.get(i)??a,this._$Em=null}}requestUpdate(t,e,r,i=!1,o){if(void 0!==t){const a=this.constructor;if(!1===i&&(o=this[t]),r??=a.getPropertyOptions(t),!((r.hasChanged??b)(o,e)||r.useDefault&&r.reflect&&o===this._$Ej?.get(t)&&!this.hasAttribute(a._$Eu(t,r))))return;this.C(t,e,r)}!1===this.isUpdatePending&&(this._$ES=this._$EP())}C(t,e,{useDefault:r,reflect:i,wrapped:o},a){r&&!(this._$Ej??=/* @__PURE__ */new Map).has(t)&&(this._$Ej.set(t,a??e??this[t]),!0!==o||void 0!==a)||(this._$AL.has(t)||(this.hasUpdated||r||(e=void 0),this._$AL.set(t,e)),!0===i&&this._$Em!==t&&(this._$Eq??=/* @__PURE__ */new Set).add(t))}async _$EP(){this.isUpdatePending=!0;try{await this._$ES}catch(t){Promise.reject(t)}const t=this.scheduleUpdate();return null!=t&&await t,!this.isUpdatePending}scheduleUpdate(){return this.performUpdate()}performUpdate(){if(!this.isUpdatePending)return;if(!this.hasUpdated){if(this.renderRoot??=this.createRenderRoot(),this._$Ep){for(const[t,e]of this._$Ep)this[t]=e;this._$Ep=void 0}const t=this.constructor.elementProperties;if(t.size>0)for(const[e,r]of t){const{wrapped:t}=r,i=this[e];!0!==t||this._$AL.has(e)||void 0===i||this.C(e,void 0,r,i)}}let t=!1;const e=this._$AL;try{t=this.shouldUpdate(e),t?(this.willUpdate(e),this._$EO?.forEach(t=>t.hostUpdate?.()),this.update(e)):this._$EM()}catch(e){throw t=!1,this._$EM(),e}t&&this._$AE(e)}willUpdate(t){}_$AE(t){this._$EO?.forEach(t=>t.hostUpdated?.()),this.hasUpdated||(this.hasUpdated=!0,this.firstUpdated(t)),this.updated(t)}_$EM(){this._$AL=/* @__PURE__ */new Map,this.isUpdatePending=!1}get updateComplete(){return this.getUpdateComplete()}getUpdateComplete(){return this._$ES}shouldUpdate(t){return!0}update(t){this._$Eq&&=this._$Eq.forEach(t=>this._$ET(t,this[t])),this._$EM()}updated(t){}firstUpdated(t){}};_.elementStyles=[],_.shadowRootOptions={mode:"open"},_[g("elementProperties")]=/* @__PURE__ */new Map,_[g("finalized")]=/* @__PURE__ */new Map,v?.({ReactiveElement:_}),(p.reactiveElementVersions??=[]).push("2.1.2");var w=globalThis,x=t=>t,A=w.trustedTypes,E=A?A.createPolicy("lit-html",{createHTML:t=>t}):void 0,S="$lit$",k=`lit$${Math.random().toFixed(9).slice(2)}$`,C="?"+k,P=`<${C}>`,O=document,z=()=>O.createComment(""),U=t=>null===t||"object"!=typeof t&&"function"!=typeof t,M=Array.isArray,T="[ \t\n\f\r]",I=/<(?:(!--|\/[^a-zA-Z])|(\/?[a-zA-Z][^>\s]*)|(\/?$))/g,F=/-->/g,B=/>/g,N=RegExp(`>|${T}(?:([^\\s"'>=/]+)(${T}*=${T}*(?:[^ \t\n\f\r"'\`<>=]|("|')|))|$)`,"g"),R=/'/g,V=/"/g,H=/^(?:script|style|textarea|title)$/i,j=t=>(e,...r)=>({_$litType$:t,strings:e,values:r}),D=j(1),q=(j(2),j(3),Symbol.for("lit-noChange")),L=Symbol.for("lit-nothing"),W=/* @__PURE__ */new WeakMap,X=O.createTreeWalker(O,129);function J(t,e){if(!M(t)||!t.hasOwnProperty("raw"))throw Error("invalid template strings array");return void 0!==E?E.createHTML(e):e}var Z=(t,e)=>{const r=t.length-1,i=[];let o,a=2===e?"<svg>":3===e?"<math>":"",s=I;for(let n=0;n<r;n++){const e=t[n];let r,h,d=-1,l=0;for(;l<e.length&&(s.lastIndex=l,h=s.exec(e),null!==h);)l=s.lastIndex,s===I?"!--"===h[1]?s=F:void 0!==h[1]?s=B:void 0!==h[2]?(H.test(h[2])&&(o=RegExp("</"+h[2],"g")),s=N):void 0!==h[3]&&(s=N):s===N?">"===h[0]?(s=o??I,d=-1):void 0===h[1]?d=-2:(d=s.lastIndex-h[2].length,r=h[1],s=void 0===h[3]?N:'"'===h[3]?V:R):s===V||s===R?s=N:s===F||s===B?s=I:(s=N,o=void 0);const c=s===N&&t[n+1].startsWith("/>")?" ":"";a+=s===I?e+P:d>=0?(i.push(r),e.slice(0,d)+S+e.slice(d)+k+c):e+k+(-2===d?n:c)}return[J(t,a+(t[r]||"<?>")+(2===e?"</svg>":3===e?"</math>":"")),i]},G=class t{constructor({strings:e,_$litType$:r},i){let o;this.parts=[];let a=0,s=0;const n=e.length-1,h=this.parts,[d,l]=Z(e,r);if(this.el=t.createElement(d,i),X.currentNode=this.el.content,2===r||3===r){const t=this.el.content.firstChild;t.replaceWith(...t.childNodes)}for(;null!==(o=X.nextNode())&&h.length<n;){if(1===o.nodeType){if(o.hasAttributes())for(const t of o.getAttributeNames())if(t.endsWith(S)){const e=l[s++],r=o.getAttribute(t).split(k),i=/([.?@])?(.*)/.exec(e);h.push({type:1,index:a,name:i[2],strings:r,ctor:"."===i[1]?et:"?"===i[1]?rt:"@"===i[1]?it:tt}),o.removeAttribute(t)}else t.startsWith(k)&&(h.push({type:6,index:a}),o.removeAttribute(t));if(H.test(o.tagName)){const t=o.textContent.split(k),e=t.length-1;if(e>0){o.textContent=A?A.emptyScript:"";for(let r=0;r<e;r++)o.append(t[r],z()),X.nextNode(),h.push({type:2,index:++a});o.append(t[e],z())}}}else if(8===o.nodeType)if(o.data===C)h.push({type:2,index:a});else{let t=-1;for(;-1!==(t=o.data.indexOf(k,t+1));)h.push({type:7,index:a}),t+=k.length-1}a++}}static createElement(t,e){const r=O.createElement("template");return r.innerHTML=t,r}};function K(t,e,r=t,i){if(e===q)return e;let o=void 0!==i?r._$Co?.[i]:r._$Cl;const a=U(e)?void 0:e._$litDirective$;return o?.constructor!==a&&(o?._$AO?.(!1),void 0===a?o=void 0:(o=new a(t),o._$AT(t,r,i)),void 0!==i?(r._$Co??=[])[i]=o:r._$Cl=o),void 0!==o&&(e=K(t,o._$AS(t,e.values),o,i)),e}var Y=class{constructor(t,e){this._$AV=[],this._$AN=void 0,this._$AD=t,this._$AM=e}get parentNode(){return this._$AM.parentNode}get _$AU(){return this._$AM._$AU}u(t){const{el:{content:e},parts:r}=this._$AD,i=(t?.creationScope??O).importNode(e,!0);X.currentNode=i;let o=X.nextNode(),a=0,s=0,n=r[0];for(;void 0!==n;){if(a===n.index){let e;2===n.type?e=new Q(o,o.nextSibling,this,t):1===n.type?e=new n.ctor(o,n.name,n.strings,this,t):6===n.type&&(e=new ot(o,this,t)),this._$AV.push(e),n=r[++s]}a!==n?.index&&(o=X.nextNode(),a++)}return X.currentNode=O,i}p(t){let e=0;for(const r of this._$AV)void 0!==r&&(void 0!==r.strings?(r._$AI(t,r,e),e+=r.strings.length-2):r._$AI(t[e])),e++}},Q=class t{get _$AU(){return this._$AM?._$AU??this._$Cv}constructor(t,e,r,i){this.type=2,this._$AH=L,this._$AN=void 0,this._$AA=t,this._$AB=e,this._$AM=r,this.options=i,this._$Cv=i?.isConnected??!0}get parentNode(){let t=this._$AA.parentNode;const e=this._$AM;return void 0!==e&&11===t?.nodeType&&(t=e.parentNode),t}get startNode(){return this._$AA}get endNode(){return this._$AB}_$AI(t,e=this){t=K(this,t,e),U(t)?t===L||null==t||""===t?(this._$AH!==L&&this._$AR(),this._$AH=L):t!==this._$AH&&t!==q&&this._(t):void 0!==t._$litType$?this.$(t):void 0!==t.nodeType?this.T(t):(t=>M(t)||"function"==typeof t?.[Symbol.iterator])(t)?this.k(t):this._(t)}O(t){return this._$AA.parentNode.insertBefore(t,this._$AB)}T(t){this._$AH!==t&&(this._$AR(),this._$AH=this.O(t))}_(t){this._$AH!==L&&U(this._$AH)?this._$AA.nextSibling.data=t:this.T(O.createTextNode(t)),this._$AH=t}$(t){const{values:e,_$litType$:r}=t,i="number"==typeof r?this._$AC(t):(void 0===r.el&&(r.el=G.createElement(J(r.h,r.h[0]),this.options)),r);if(this._$AH?._$AD===i)this._$AH.p(e);else{const t=new Y(i,this),r=t.u(this.options);t.p(e),this.T(r),this._$AH=t}}_$AC(t){let e=W.get(t.strings);return void 0===e&&W.set(t.strings,e=new G(t)),e}k(e){M(this._$AH)||(this._$AH=[],this._$AR());const r=this._$AH;let i,o=0;for(const a of e)o===r.length?r.push(i=new t(this.O(z()),this.O(z()),this,this.options)):i=r[o],i._$AI(a),o++;o<r.length&&(this._$AR(i&&i._$AB.nextSibling,o),r.length=o)}_$AR(t=this._$AA.nextSibling,e){for(this._$AP?.(!1,!0,e);t!==this._$AB;){const e=x(t).nextSibling;x(t).remove(),t=e}}setConnected(t){void 0===this._$AM&&(this._$Cv=t,this._$AP?.(t))}},tt=class{get tagName(){return this.element.tagName}get _$AU(){return this._$AM._$AU}constructor(t,e,r,i,o){this.type=1,this._$AH=L,this._$AN=void 0,this.element=t,this.name=e,this._$AM=i,this.options=o,r.length>2||""!==r[0]||""!==r[1]?(this._$AH=Array(r.length-1).fill(/* @__PURE__ */new String),this.strings=r):this._$AH=L}_$AI(t,e=this,r,i){const o=this.strings;let a=!1;if(void 0===o)t=K(this,t,e,0),a=!U(t)||t!==this._$AH&&t!==q,a&&(this._$AH=t);else{const i=t;let s,n;for(t=o[0],s=0;s<o.length-1;s++)n=K(this,i[r+s],e,s),n===q&&(n=this._$AH[s]),a||=!U(n)||n!==this._$AH[s],n===L?t=L:t!==L&&(t+=(n??"")+o[s+1]),this._$AH[s]=n}a&&!i&&this.j(t)}j(t){t===L?this.element.removeAttribute(this.name):this.element.setAttribute(this.name,t??"")}},et=class extends tt{constructor(){super(...arguments),this.type=3}j(t){this.element[this.name]=t===L?void 0:t}},rt=class extends tt{constructor(){super(...arguments),this.type=4}j(t){this.element.toggleAttribute(this.name,!!t&&t!==L)}},it=class extends tt{constructor(t,e,r,i,o){super(t,e,r,i,o),this.type=5}_$AI(t,e=this){if((t=K(this,t,e,0)??L)===q)return;const r=this._$AH,i=t===L&&r!==L||t.capture!==r.capture||t.once!==r.once||t.passive!==r.passive,o=t!==L&&(r===L||i);i&&this.element.removeEventListener(this.name,this,r),o&&this.element.addEventListener(this.name,this,t),this._$AH=t}handleEvent(t){"function"==typeof this._$AH?this._$AH.call(this.options?.host??this.element,t):this._$AH.handleEvent(t)}},ot=class{constructor(t,e,r){this.element=t,this.type=6,this._$AN=void 0,this._$AM=e,this.options=r}get _$AU(){return this._$AM._$AU}_$AI(t){K(this,t)}},at=w.litHtmlPolyfillSupport;at?.(G,Q),(w.litHtmlVersions??=[]).push("3.3.3");var st=globalThis,nt=class extends _{constructor(){super(...arguments),this.renderOptions={host:this},this._$Do=void 0}createRenderRoot(){const t=super.createRenderRoot();return this.renderOptions.renderBefore??=t.firstChild,t}update(t){const e=this.render();this.hasUpdated||(this.renderOptions.isConnected=this.isConnected),super.update(t),this._$Do=((t,e,r)=>{const i=r?.renderBefore??e;let o=i._$litPart$;if(void 0===o){const t=r?.renderBefore??null;i._$litPart$=o=new Q(e.insertBefore(z(),t),t,void 0,r??{})}return o._$AI(t),o})(e,this.renderRoot,this.renderOptions)}connectedCallback(){super.connectedCallback(),this._$Do?.setConnected(!0)}disconnectedCallback(){super.disconnectedCallback(),this._$Do?.setConnected(!1)}render(){return q}};nt._$litElement$=!0,nt.finalized=!0,st.litElementHydrateSupport?.({LitElement:nt});var ht=st.litElementPolyfillSupport;ht?.({LitElement:nt}),(st.litElementVersions??=[]).push("4.2.2");var dt=t=>(e,r)=>{void 0!==r?r.addInitializer(()=>{customElements.define(t,e)}):customElements.define(t,e)},lt={attribute:!0,type:String,converter:y,reflect:!1,hasChanged:b},ct=(t=lt,e,r)=>{const{kind:i,metadata:o}=r;let a=globalThis.litPropertyMetadata.get(o);if(void 0===a&&globalThis.litPropertyMetadata.set(o,a=/* @__PURE__ */new Map),"setter"===i&&((t=Object.create(t)).wrapped=!0),a.set(r.name,t),"accessor"===i){const{name:i}=r;return{set(r){const o=e.get.call(this);e.set.call(this,r),this.requestUpdate(i,o,t,!0,r)},init(e){return void 0!==e&&this.C(i,void 0,t,e),e}}}if("setter"===i){const{name:i}=r;return function(r){const o=this[i];e.call(this,r),this.requestUpdate(i,o,t,!0,r)}}throw Error("Unsupported decorator location: "+i)};function ut(t){return(e,r)=>"object"==typeof r?ct(t,e,r):((t,e,r)=>{const i=e.hasOwnProperty(r);return e.constructor.createProperty(r,t),i?Object.getOwnPropertyDescriptor(e,r):void 0})(t,e,r)}function pt(t){return ut({...t,state:!0,attribute:!1})}var mt=class{constructor(t,e){this.config=null,this.loading=!1,this.error=null,this.host=t,this.configUrl=e,t.addController(this)}hostConnected(){}hostDisconnected(){this.abortController?.abort()}async loadConfig(){this.loading=!0,this.error=null,this.abortController=new AbortController,this.host.requestUpdate();try{const t=await fetch(this.configUrl,{headers:{Accept:"application/json"},credentials:"same-origin",signal:this.abortController.signal});if(!t.ok)throw new Error(`Failed to load configuration: ${t.statusText}`);this.config=await t.json(),this.validateConfig(this.config)}catch(E){if(E instanceof Error&&"AbortError"===E.name)return;this.error=E,this.config=null}finally{this.loading=!1,this.host.requestUpdate()}}validateConfig(t){if(!t||"object"!=typeof t)throw new Error("Invalid configuration format");if(!Array.isArray(t.primaryAuthenticators))throw new Error("Configuration must include primaryAuthenticators array");if(0===t.primaryAuthenticators.length)throw new Error("At least one primary authentication method must be configured");if(!Array.isArray(t.secondaryAuthenticators))throw new Error("Configuration must include secondaryAuthenticators array")}getEnabledPrimaryAuthenticators(){return this.config?.primaryAuthenticators.filter(t=>t.enabled)||[]}getPrimaryMethodByType(t){return this.config?.primaryAuthenticators.find(e=>e.type===t&&e.enabled)}getSecondaryAuthenticator(t){return this.config?.secondaryAuthenticators.find(e=>e.type===t)}},ft=a`
  :host {
    display: block;
    
    /* Colors - Duende-inspired palette with CSS custom properties */
    --auth-primary-color: var(--duende-primary, #5B6FED);
    --auth-primary-hover: var(--duende-primary-hover, #4557D2);
    --auth-primary-light: var(--duende-primary-light, #EEF0FF);
    --auth-text-primary: var(--duende-text, #1F2937);
    --auth-text-secondary: var(--duende-text-secondary, #6B7280);
    --auth-text-muted: var(--duende-text-muted, #9CA3AF);
    --auth-border-color: var(--duende-border, #E5E7EB);
    --auth-border-focus: var(--duende-border-focus, #5B6FED);
    --auth-background: var(--duende-background, #FFFFFF);
    --auth-background-secondary: var(--duende-background-secondary, #F9FAFB);
    --auth-error-color: var(--duende-error, #EF4444);
    --auth-error-background: var(--duende-error-background, #FEF2F2);
    --auth-success-color: var(--duende-success, #10B981);
    --auth-success-background: var(--duende-success-background, #ECFDF5);
    
    /* Spacing */
    --auth-spacing-xs: var(--spacing-xs, 0.5rem);
    --auth-spacing-sm: var(--spacing-sm, 0.75rem);
    --auth-spacing-md: var(--spacing-md, 1rem);
    --auth-spacing-lg: var(--spacing-lg, 1.5rem);
    --auth-spacing-xl: var(--spacing-xl, 2rem);
    --auth-spacing-2xl: var(--spacing-2xl, 2.5rem);
    
    /* Typography */
    --auth-font-family: var(--font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif);
    --auth-font-size-xs: var(--font-size-xs, 0.75rem);
    --auth-font-size-sm: var(--font-size-sm, 0.875rem);
    --auth-font-size-base: var(--font-size-base, 1rem);
    --auth-font-size-lg: var(--font-size-lg, 1.125rem);
    --auth-font-size-xl: var(--font-size-xl, 1.5rem);
    --auth-font-size-2xl: var(--font-size-2xl, 1.875rem);
    --auth-font-weight-normal: var(--font-weight-normal, 400);
    --auth-font-weight-medium: var(--font-weight-medium, 500);
    --auth-font-weight-semibold: var(--font-weight-semibold, 600);
    --auth-line-height-tight: var(--line-height-tight, 1.25);
    --auth-line-height-normal: var(--line-height-normal, 1.5);
    
    /* Layout */
    --auth-border-radius: var(--border-radius, 0.5rem);
    --auth-border-radius-lg: var(--border-radius-lg, 0.75rem);
    --auth-border-width: var(--border-width, 1px);
    --auth-max-width: var(--max-width, 400px);
    --auth-shadow-sm: var(--shadow-sm, 0 1px 2px 0 rgb(0 0 0 / 0.05));
    --auth-shadow: var(--shadow, 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1));
    --auth-shadow-lg: var(--shadow-lg, 0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1));
    --auth-focus-ring: var(--focus-ring, 0 0 0 3px var(--auth-primary-light));
    
    /* Transitions */
    --auth-transition-fast: var(--transition-fast, 150ms ease-in-out);
    --auth-transition-base: var(--transition-base, 200ms ease-in-out);
    
    font-family: var(--auth-font-family);
    color: var(--auth-text-primary);
  }

  .auth-container {
    max-width: var(--auth-max-width);
    margin: 0 auto;
    padding: var(--auth-spacing-md);
  }

  .auth-logo {
    display: flex;
    justify-content: center;
    margin-bottom: var(--auth-spacing-xl);
  }

  .auth-logo svg,
  .auth-logo img {
    width: 64px;
    height: 64px;
  }

  .default-logo {
    width: 64px;
    height: 64px;
    background: linear-gradient(135deg, var(--auth-primary-color) 0%, var(--auth-primary-hover) 100%);
    border-radius: var(--auth-border-radius-lg);
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: var(--auth-shadow);
  }

  .default-logo::before {
    content: '';
    width: 32px;
    height: 32px;
    background: white;
    border-radius: var(--auth-border-radius);
    opacity: 0.9;
  }

  .auth-header {
    text-align: center;
    margin-bottom: var(--auth-spacing-xl);
  }

  .auth-form {
    background: var(--auth-background);
    border-radius: var(--auth-border-radius-lg);
    padding: var(--auth-spacing-2xl);
    box-shadow: var(--auth-shadow-lg);
    border: var(--auth-border-width) solid var(--auth-border-color);
  }

  .auth-form h3 {
    margin: 0 0 var(--auth-spacing-xs) 0;
    font-size: var(--auth-font-size-2xl);
    font-weight: var(--auth-font-weight-semibold);
    text-align: center;
    line-height: var(--auth-line-height-tight);
    color: var(--auth-text-primary);
  }

  .form-description {
    color: var(--auth-text-secondary);
    margin-bottom: var(--auth-spacing-lg);
    text-align: center;
    font-size: var(--auth-font-size-sm);
    line-height: var(--auth-line-height-normal);
  }

  .form-field {
    margin-bottom: var(--auth-spacing-md);
  }

  .form-field label {
    display: block;
    margin-bottom: var(--auth-spacing-xs);
    font-weight: var(--auth-font-weight-medium);
    font-size: var(--auth-font-size-sm);
    color: var(--auth-text-primary);
  }

  .form-field input {
    width: 100%;
    padding: 0.75rem;
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius);
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    box-sizing: border-box;
    transition: all var(--auth-transition-base);
    background: var(--auth-background);
    color: var(--auth-text-primary);
  }

  .form-field input::placeholder {
    color: var(--auth-text-muted);
  }

  .form-field input:hover {
    border-color: var(--auth-text-secondary);
  }

  .form-field input:focus {
    outline: none;
    border-color: var(--auth-border-focus);
    box-shadow: var(--auth-focus-ring);
  }

  .form-field input:disabled {
    background: var(--auth-background-secondary);
    color: var(--auth-text-muted);
    cursor: not-allowed;
  }

  .form-checkbox {
    display: flex;
    align-items: center;
    gap: var(--auth-spacing-xs);
    margin-bottom: var(--auth-spacing-lg);
  }

  .form-checkbox input[type="checkbox"] {
    width: auto;
    cursor: pointer;
    accent-color: var(--auth-primary-color);
  }

  .form-checkbox label {
    font-size: var(--auth-font-size-sm);
    cursor: pointer;
    margin: 0;
  }

  .btn-primary {
    width: 100%;
    padding: 0.875rem;
    background: var(--auth-primary-color);
    color: white;
    border: none;
    border-radius: var(--auth-border-radius);
    font-size: var(--auth-font-size-base);
    font-weight: var(--auth-font-weight-semibold);
    font-family: var(--auth-font-family);
    cursor: pointer;
    transition: all var(--auth-transition-base);
    box-shadow: var(--auth-shadow-sm);
  }

  .btn-primary:hover {
    background: var(--auth-primary-hover);
    box-shadow: var(--auth-shadow);
    transform: translateY(-1px);
  }

  .btn-primary:active {
    transform: translateY(0);
  }

  .btn-primary:disabled {
    background: var(--auth-text-muted);
    cursor: not-allowed;
    transform: none;
    box-shadow: none;
    opacity: 0.6;
  }

  .btn-secondary {
    width: 100%;
    padding: 0.875rem;
    background: var(--auth-background);
    color: var(--auth-primary-color);
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius);
    font-size: var(--auth-font-size-base);
    font-weight: var(--auth-font-weight-medium);
    font-family: var(--auth-font-family);
    cursor: pointer;
    transition: all var(--auth-transition-base);
  }

  .btn-secondary:hover {
    background: var(--auth-background-secondary);
    border-color: var(--auth-primary-color);
  }

  .btn-secondary:disabled {
    border-color: var(--auth-border-color);
    color: var(--auth-text-muted);
    cursor: not-allowed;
    opacity: 0.6;
  }

  .btn-secondary:disabled:hover {
    background: var(--auth-background);
    border-color: var(--auth-border-color);
  }

  .btn-link {
    background: none;
    border: none;
    color: var(--auth-primary-color);
    cursor: pointer;
    text-decoration: none;
    padding: var(--auth-spacing-xs);
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    transition: color var(--auth-transition-fast);
  }

  .btn-link:hover {
    color: var(--auth-primary-hover);
    text-decoration: underline;
  }

  .error-message {
    padding: var(--auth-spacing-md);
    background: var(--auth-error-background);
    border: var(--auth-border-width) solid var(--auth-error-color);
    border-radius: var(--auth-border-radius);
    color: var(--auth-error-color);
    margin-bottom: var(--auth-spacing-md);
    font-size: var(--auth-font-size-sm);
    line-height: var(--auth-line-height-normal);
  }

  .success {
    padding: var(--auth-spacing-2xl);
    text-align: center;
    color: var(--auth-success-color);
    font-weight: var(--auth-font-weight-medium);
    font-size: var(--auth-font-size-lg);
  }

  .loading {
    padding: var(--auth-spacing-2xl);
    text-align: center;
    color: var(--auth-text-secondary);
    font-size: var(--auth-font-size-base);
  }

  .provider-buttons {
    display: flex;
    flex-direction: column;
    gap: var(--auth-spacing-sm);
  }

  .provider-buttons-inline {
    display: flex;
    flex-direction: column;
    gap: var(--auth-spacing-sm);
  }

  .provider-button {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: var(--auth-spacing-sm);
    padding: 0.875rem;
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius);
    background: var(--auth-background);
    cursor: pointer;
    transition: all var(--auth-transition-base);
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    font-weight: var(--auth-font-weight-medium);
    width: 100%;
    color: var(--auth-text-primary);
  }

  .provider-button:hover {
    background: var(--auth-background-secondary);
    border-color: var(--auth-text-secondary);
    box-shadow: var(--auth-shadow-sm);
  }

  .provider-button span {
    display: flex;
    align-items: center;
    gap: var(--auth-spacing-xs);
  }

  .method-selection {
    display: flex;
    flex-direction: column;
    gap: var(--auth-spacing-md);
  }

  .method-button {
    padding: var(--auth-spacing-lg);
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius-lg);
    background: var(--auth-background);
    cursor: pointer;
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    transition: all var(--auth-transition-base);
    text-align: left;
  }

  .method-button:hover {
    border-color: var(--auth-primary-color);
    background: var(--auth-background-secondary);
    box-shadow: var(--auth-shadow-sm);
  }

  .method-button-title {
    font-weight: var(--auth-font-weight-semibold);
    margin-bottom: var(--auth-spacing-xs);
    color: var(--auth-text-primary);
  }

  .method-button-description {
    color: var(--auth-text-secondary);
    font-size: var(--auth-font-size-sm);
    line-height: var(--auth-line-height-normal);
  }

  .form-footer {
    margin-top: var(--auth-spacing-lg);
    text-align: center;
    padding-top: var(--auth-spacing-md);
    border-top: var(--auth-border-width) solid var(--auth-border-color);
  }

  .link-button {
    background: none;
    border: none;
    color: var(--auth-primary-color);
    cursor: pointer;
    text-decoration: none;
    padding: var(--auth-spacing-xs);
    font-size: var(--auth-font-size-sm);
    font-family: var(--auth-font-family);
    transition: color var(--auth-transition-fast);
  }

  .link-button:hover {
    color: var(--auth-primary-hover);
    text-decoration: underline;
  }

  .form-field small {
    display: block;
    margin-top: var(--auth-spacing-xs);
    color: var(--auth-text-secondary);
    font-size: var(--auth-font-size-xs);
    line-height: var(--auth-line-height-normal);
  }

  .back-button {
    background: none;
    border: none;
    color: var(--auth-primary-color);
    cursor: pointer;
    padding: var(--auth-spacing-xs) 0;
    margin-bottom: var(--auth-spacing-md);
    font-size: var(--auth-font-size-sm);
    font-family: var(--auth-font-family);
    display: flex;
    align-items: center;
    gap: var(--auth-spacing-xs);
    transition: color var(--auth-transition-fast);
    font-weight: var(--auth-font-weight-medium);
  }

  .back-button:hover {
    color: var(--auth-primary-hover);
  }

  .divider {
    display: flex;
    align-items: center;
    text-align: center;
    margin: var(--auth-spacing-lg) 0;
    color: var(--auth-text-muted);
    font-size: var(--auth-font-size-xs);
    text-transform: uppercase;
    font-weight: var(--auth-font-weight-medium);
    letter-spacing: 0.05em;
  }

  .divider::before,
  .divider::after {
    content: '';
    flex: 1;
    border-bottom: var(--auth-border-width) solid var(--auth-border-color);
  }

  .divider span {
    padding: 0 var(--auth-spacing-md);
  }

  /* Accessibility improvements */
  @media (prefers-reduced-motion: reduce) {
    * {
      transition: none !important;
      animation: none !important;
    }
  }

  /* Focus visible for keyboard navigation */
  *:focus-visible {
    outline: 2px solid var(--auth-primary-color);
    outline-offset: 2px;
  }
`;function vt(t,e,r,i){var o,a=arguments.length,s=a<3?e:null===i?i=Object.getOwnPropertyDescriptor(e,r):i;if("object"==typeof Reflect&&"function"==typeof Reflect.decorate)s=Reflect.decorate(t,e,r,i);else for(var n=t.length-1;n>=0;n--)(o=t[n])&&(s=(a<3?o(s):a>3?o(e,r,s):o(e,r))||s);return a>3&&s&&Object.defineProperty(e,r,s),s}var gt=class extends nt{constructor(...t){super(...t),this.message=""}static{this.styles=ft}render(){return this.message?D`
      <div part="error" class="error-message" role="alert">
        ${this.message}
      </div>
    `:""}};vt([ut({type:String})],gt.prototype,"message",void 0),gt=vt([dt("auth-error-display")],gt);var yt=class extends nt{constructor(...t){super(...t),this.emailValue="",this.disabled=!1,this.showEmailInput=!0,this.isSecondaryButton=!1}static{this.styles=ft}render(){return this.showEmailInput?this._renderForm():this._renderButton()}_renderForm(){return D`
      <form part="form" @submit=${this._handleSubmit}>
        <div part="field" class="form-field">
          <label part="label" for="email">Email</label>
          <input
            part="input"
            type="email"
            id="email"
            name="email"
            required
            autocomplete="email"
            placeholder="user@domain.com"
            .value=${this.emailValue}
            @input=${this._handleInput}
            ?disabled=${this.disabled}
          />
        </div>
        
        <button part="primary-button" type="submit" class="btn-primary" ?disabled=${this.disabled}>
          ${this.disabled?"Sending code...":"Email me a code"}
        </button>
      </form>
    `}_handleInput(t){const e=t.target;this.emailValue=e.value}_renderButton(){const t=this.isSecondaryButton?"btn-secondary":"btn-primary",e=this.isSecondaryButton?"secondary-button":"primary-button";return D`
      <button 
        part="${e}"
        class="${t}" 
        @click=${this._handleClick}
        ?disabled=${this.disabled}
      >
        ${this.disabled?"Sending code...":"Email me a code"}
      </button>
    `}_handleSubmit(t){t.preventDefault(),t.target;const e=this.renderRoot.querySelector('input[name="email"]').value;this.dispatchEvent(new CustomEvent("otp-send",{detail:{email:e},bubbles:!0,composed:!0}))}_handleClick(){this.dispatchEvent(new CustomEvent("otp-send",{detail:{email:this.emailValue},bubbles:!0,composed:!0}))}};vt([ut({type:String})],yt.prototype,"emailValue",void 0),vt([ut({type:Boolean})],yt.prototype,"disabled",void 0),vt([ut({type:Boolean})],yt.prototype,"showEmailInput",void 0),vt([ut({type:Boolean})],yt.prototype,"isSecondaryButton",void 0),yt=vt([dt("auth-otp-send")],yt);var bt=class extends nt{constructor(...t){super(...t),this.providers=[]}static{this.styles=ft}render(){return this.providers&&0!==this.providers.length?D`
      <div part="providers" class="provider-buttons-inline">
        ${this.providers.map(t=>D`
          <button
            part="provider-button"
            class="provider-button"
            @click=${()=>this._handleProviderClick(t)}
          >
            <span>Continue with ${t.displayName}</span>
          </button>
        `)}
      </div>
    `:""}_handleProviderClick(t){this.dispatchEvent(new CustomEvent("provider-select",{detail:{provider:t},bubbles:!0,composed:!0}))}};vt([ut({type:Array})],bt.prototype,"providers",void 0),bt=vt([dt("auth-provider-buttons")],bt);var $t=class extends nt{constructor(...t){super(...t),this.error="",this.emailValue="",this.showOtpAuth=!1,this.showOtpEmailInput=!1,this.externalProviders=[],this.disabled=!1}static{this.styles=ft}render(){const t=this.showOtpAuth&&this.externalProviders.length>0;return D`
      <div part="card" class="auth-form">
        <h3 part="form-title">Sign In</h3>
        
        <auth-error-display .message=${this.error}></auth-error-display>
        
        ${this.showOtpAuth?D`
          <auth-otp-send
            .emailValue=${this.emailValue}
            .showEmailInput=${this.showOtpEmailInput}
            .isSecondaryButton=${!1}
            .disabled=${this.disabled}
            @otp-send=${this._handleOtpSend}
          ></auth-otp-send>
        `:""}
        
        ${t?D`
          <div part="divider" class="divider">
            <span>or</span>
          </div>
        `:""}
        
        ${this.externalProviders.length>0?D`
          <auth-provider-buttons
            .providers=${this.externalProviders}
            @provider-select=${this._handleProviderSelect}
          ></auth-provider-buttons>
        `:""}
      </div>
    `}_handleOtpSend(t){}_handleProviderSelect(t){}};vt([ut({type:String})],$t.prototype,"error",void 0),vt([ut({type:String})],$t.prototype,"emailValue",void 0),vt([ut({type:Boolean})],$t.prototype,"showOtpAuth",void 0),vt([ut({type:Boolean})],$t.prototype,"showOtpEmailInput",void 0),vt([ut({type:Array})],$t.prototype,"externalProviders",void 0),vt([ut({type:Boolean})],$t.prototype,"disabled",void 0),$t=vt([dt("auth-unified-form")],$t);var _t=class extends nt{constructor(...t){super(...t),this.email=""}static{this.styles=[ft,a`
      .otp-input {
        text-transform: uppercase;
        letter-spacing: 0.1em;
        font-size: 1.2rem;
        text-align: center;
      }
    `]}render(){return D`
      <h3 part="form-title">Enter Verification Code</h3>
      <p part="description" class="form-description">
        We've sent a verification code to <strong>${this.email}</strong>
      </p>
      
      <form part="form" @submit=${this._handleSubmit}>
        <div part="field" class="form-field">
          <input
            part="input"
            class="otp-input"
            type="text"
            id="otp-code"
            name="code"
            placeholder="XXXX-XXXX"
            autocomplete="one-time-code"
            required
            maxlength="9"
            @input=${this._handleFormatInput}
          />
        </div>
        
        <button part="primary-button" type="submit" class="btn-primary">
          Verify Code
        </button>
      </form>
    `}_handleSubmit(t){t.preventDefault();const e=t.target,r=new FormData(e);this.dispatchEvent(new CustomEvent("otp-verify",{detail:{code:r.get("code")},bubbles:!0,composed:!0}))}_handleFormatInput(t){const e=t.target;let r=e.value.replace(/[^0-9A-Za-z]/g,"").toUpperCase();r.length>4&&(r=r.slice(0,4)+"-"+r.slice(4,8)),e.value=r}};vt([ut({type:String})],_t.prototype,"email",void 0),_t=vt([dt("auth-otp-form")],_t);var wt=class extends nt{constructor(...t){super(...t),this.error="",this.email="",this.title="Verify Email",this.description=""}static{this.styles=ft}render(){const t=this.description||`We sent a verification code to ${this.email}. Please enter it below.`;return D`
      <div part="card" class="auth-form">
        <button 
          type="button" 
          class="back-button" 
          @click=${this._handleBack}
          aria-label="Go back to sign in"
        >
          ← Back
        </button>
        
        <h3 part="form-title">${this.title}</h3>
        
        ${t?D`
          <p part="description" class="description">${t}</p>
        `:""}
        
        <auth-error-display .message=${this.error}></auth-error-display>
        
        <auth-otp-form
          .email=${this.email}
          @otp-verify=${this._handleOtpVerify}
        ></auth-otp-form>
      </div>
    `}_handleOtpVerify(t){}_handleBack(){this.dispatchEvent(new CustomEvent("back-click",{bubbles:!0,composed:!0}))}};vt([ut({type:String})],wt.prototype,"error",void 0),vt([ut({type:String})],wt.prototype,"email",void 0),vt([ut({type:String})],wt.prototype,"title",void 0),vt([ut({type:String})],wt.prototype,"description",void 0),wt=vt([dt("auth-otp-card")],wt);var xt=class extends nt{static{this.styles=[ft,a`
      .mfa-input {
        letter-spacing: 0.5em;
        font-size: 1.5rem;
        text-align: center;
      }
    `]}render(){return D`
      <h3 part="form-title">Two-Factor Authentication</h3>
      <p part="description" class="form-description">
        Enter the 6-digit code from your authenticator app
      </p>
      
      <form part="form" @submit=${this._handleSubmit}>
        <div part="field" class="form-field">
          <input
            part="input"
            class="mfa-input"
            type="text"
            id="mfa-code"
            name="code"
            placeholder="000000"
            autocomplete="one-time-code"
            required
            maxlength="6"
            inputmode="numeric"
            pattern="[0-9]{6}"
          />
        </div>
        
        <button part="primary-button" type="submit" class="btn-primary">
          Verify Code
        </button>
      </form>
    `}_handleSubmit(t){t.preventDefault();const e=t.target,r=new FormData(e);this.dispatchEvent(new CustomEvent("mfa-verify",{detail:{code:r.get("code")},bubbles:!0,composed:!0}))}};xt=vt([dt("auth-mfa-form")],xt);var At=class extends nt{constructor(...t){super(...t),this.error=""}static{this.styles=ft}render(){return D`
      <div part="card" class="auth-form">
        <button 
          type="button" 
          class="back-button" 
          @click=${this._handleBack}
          aria-label="Go back to sign in"
        >
          Back
        </button>
        
        <auth-error-display .message=${this.error}></auth-error-display>
        
        <auth-mfa-form
          @mfa-verify=${this._handleMfaVerify}
        ></auth-mfa-form>
      </div>
    `}_handleMfaVerify(t){}_handleBack(){this.dispatchEvent(new CustomEvent("back-click",{bubbles:!0,composed:!0}))}};vt([ut({type:String})],At.prototype,"error",void 0),At=vt([dt("auth-mfa-card")],At);var Et=class extends nt{static{this.styles=ft}constructor(){super(),this.configUrl="/api/auth/config",this.stateUrl="/api/auth/state",this.cancelUrl="/api/auth/cancel",this.currentState="loading-config",this.error=null,this.sessionId=null,this.email="",this.configController=new mt(this,"")}connectedCallback(){super.connectedCallback(),this.configController.configUrl=this.configUrl,this.loadConfiguration()}updated(t){super.updated(t),t.has("currentState")&&this.emitEvent("auth-state-change",{state:this.currentState})}async loadConfiguration(){this.currentState="loading-config",await this.configController.loadConfig(),this.configController.error?(this.error=this.configController.error.message,this.currentState="config-error",this.emitEvent("auth-error",{error:this.error,method:"config"})):this.configController.config&&await this.checkAuthenticationState()}async checkAuthenticationState(){try{const t=await fetch(this.stateUrl,{headers:{Accept:"application/json"},credentials:"same-origin"});if(!t.ok)throw new Error("Failed to fetch authentication state");const e=await t.json();this.handleAuthenticationState(e)}catch(t){this.error=t.message,this.currentState="config-error",this.emitEvent("auth-error",{error:this.error,method:"state"})}}handleAuthenticationState(t){this.sessionId=t.sessionId||null,this.email=t.email||"",t.authenticated?(this.currentState="authenticated",this.emitEvent("auth-success",{method:"existing-session"})):(this.currentState=t.nextState,"mfa-required"===t.nextState&&this.emitEvent("mfa-required",{sessionId:t.sessionId}))}reset(){this.error=null,this.cancelAuthenticationFlow()}async cancelAuthenticationFlow(){try{(await fetch(this.cancelUrl,{method:"POST",headers:{Accept:"application/json"},credentials:"same-origin"})).ok,this.sessionId=null,this.email="",await this.checkAuthenticationState()}catch(t){this.sessionId=null,this.email="",this.currentState="unified-form"}}async handleOtpSendDirect(t){if(t){this.email=t,this.error=null,this.currentState="sending-otp";try{const e=this.configController.getPrimaryMethodByType("otp");if(!e?.endpoints?.sendOtp)throw new Error("OTP method not configured");const r=await fetch(e.endpoints.sendOtp,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({email:t}),credentials:"same-origin"}),i=await r.json();if(!r.ok||!i.success)throw new Error(i.error||"Failed to send OTP");this.sessionId=i.sessionId,"otp-verify"===i.nextAction&&(this.currentState="otp-verify")}catch(e){this.error=e.message,this.currentState="unified-form",this.emitEvent("auth-error",{error:this.error,method:"otp"})}}else this.error="Please enter your email address"}async handleOtpVerifyDirect(t){if(t)if(t.trim().toUpperCase().replace(/-/g,"").length<8)this.error="Please enter the complete verification code";else{this.error=null;try{const e=this.getPrimaryMethodEndpoint("otp","verifyOtp"),r=await fetch(e,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({sessionId:this.sessionId,code:t}),credentials:"same-origin"}),i=await r.json();if("mfa-required"===i.nextAction)return this.sessionId=i.sessionId,this.currentState="mfa-required",void this.emitEvent("mfa-required",{sessionId:i.sessionId});if(!r.ok||!i.success)throw new Error(i.error||"Invalid code");this.handleAuthSuccess("otp")}catch(e){this.error=e.message,this.emitEvent("auth-error",{error:this.error,method:"otp"})}}else this.error="Please enter the verification code"}async handleMfaVerifyDirect(t){if(!t)return void(this.error="Please enter the verification code");const e=t.replace(/\s/g,"");if(6===e.length){this.currentState="mfa-verify",this.error=null;try{const t=this.getSecondaryAuthenticatorEndpoint("totp","verifyMfa"),r=await fetch(t,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({sessionId:this.sessionId,code:e}),credentials:"same-origin"}),i=await r.json();if(!r.ok||!i.success)throw new Error(i.error||"Invalid code");this.handleAuthSuccess("totp")}catch(r){this.error=r.message,this.currentState="mfa-required",this.emitEvent("auth-error",{error:this.error,method:"totp"})}}else this.error="Please enter all 6 digits"}_handleOtpSendClick(t){const{email:e}=t.detail;this.email=e,this.handleOtpSendDirect(e)}_handleOtpVerifyEvent(t){const{code:e}=t.detail;this.handleOtpVerifyDirect(e)}_handleMfaVerifyEvent(t){const{code:e}=t.detail;this.handleMfaVerifyDirect(e)}_handleProviderClick(t){const{provider:e}=t.detail;this.handleProviderClick(e.challengeUrl)}handleProviderClick(t){window.location.href=t}handleAuthSuccess(t){this.currentState="authenticated",this.emitEvent("auth-success",{method:t})}getPrimaryMethodEndpoint(t,e){const r=this.configController.getPrimaryMethodByType(t);if(r?.endpoints&&r.endpoints[e])return r.endpoints[e];throw new Error(`Endpoint ${e} not found for primary method ${t}`)}getSecondaryAuthenticatorEndpoint(t,e){const r=this.configController.getSecondaryAuthenticator(t);if(r?.endpoints&&r.endpoints[e])return r.endpoints[e];throw new Error(`Endpoint ${e} not found for secondary factor ${t}`)}emitEvent(t,e){this.dispatchEvent(new CustomEvent(t,{detail:e,bubbles:!0,composed:!0}))}render(){return D`
      <div part="container" class="auth-container">
        <slot name="logo"></slot>
        
        <slot name="header"></slot>
        
        ${this.renderState()}
        
        <slot name="footer"></slot>
      </div>
    `}renderState(){switch(this.currentState){case"loading-config":return D`<div class="loading">Loading...</div>`;case"config-error":return D`
          <div class="error-message">
            ${this.error||"Failed to load configuration"}
          </div>
        `;case"unified-form":case"sending-otp":return this.renderUnifiedForm();case"otp-verify":return this.renderOtpVerifyForm();case"mfa-required":return this.renderMfaForm();case"mfa-verify":return D`<div class="loading">Verifying...</div>`;case"authenticated":return D`<div class="success">Authentication successful</div>`;default:return D`<div class="error-message">Unknown state</div>`}}renderUnifiedForm(){const t=this.configController.getEnabledPrimaryAuthenticators(),e=t.find(t=>"otp"===t.type),r=t.find(t=>"external"===t.type),i="sending-otp"===this.currentState,o=r&&"providers"in r.config?r.config.providers:[];return D`
      <auth-unified-form
        .error=${this.error}
        .emailValue=${this.email}
        .showPasswordAuth=${!1}
        .showRememberMe=${!1}
        .showOtpAuth=${!!e}
        .showOtpEmailInput=${!!e}
        .showOtpAsSecondary=${!1}
        .externalProviders=${o}
        .disabled=${i}
        .showRegistrationLink=${!1}
        @otp-send=${this._handleOtpSendClick}
        @provider-select=${this._handleProviderClick}
      ></auth-unified-form>
    `}renderOtpVerifyForm(){return D`
      <auth-otp-card
        .error=${this.error||""}
        .email=${this.email}
        .title=${"Verify Email"}
        .description=${""}
        @otp-verify=${this._handleOtpVerifyEvent}
        @back-click=${this.reset}
      ></auth-otp-card>
    `}renderMfaForm(){return D`
      <auth-mfa-card
        .error=${this.error||""}
        @mfa-verify=${this._handleMfaVerifyEvent}
        @back-click=${this.reset}
      ></auth-mfa-card>
    `}};vt([ut({type:String,attribute:"config-url"})],Et.prototype,"configUrl",void 0),vt([ut({type:String,attribute:"state-url"})],Et.prototype,"stateUrl",void 0),vt([ut({type:String,attribute:"cancel-url"})],Et.prototype,"cancelUrl",void 0),vt([pt()],Et.prototype,"currentState",void 0),vt([pt()],Et.prototype,"error",void 0),vt([pt()],Et.prototype,"sessionId",void 0),vt([pt()],Et.prototype,"email",void 0),Et=vt([dt("auth-component")],Et);export{Et as AuthComponent};